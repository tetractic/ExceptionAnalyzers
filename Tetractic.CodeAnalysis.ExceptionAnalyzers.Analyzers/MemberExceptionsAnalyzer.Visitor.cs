// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    public partial class MemberExceptionsAnalyzer
    {
        internal abstract class Visitor : CSharpSyntaxWalker
        {
            protected readonly SemanticModelAnalysisContext SemanticModelContext;

            protected readonly Context Context;

            private readonly Stack<Access> _accessScopes;

            // Exception types declared on "catch" clauses and exception types that could be caught
            // by the general "catch" clause of each enclosing "try" block.
            private readonly Stack<(ExceptionTypesBuilder? CatchTypes, ExceptionTypesBuilder? GeneralCatchRethrowTypes)> _catchTypesScopes;

            // Exception types declared on each enclosing "catch" clause.
            private readonly Stack<ExceptionTypesBuilder> _rethrowTypesScopes;

            private readonly ExceptionTypesBuilder _builder;

            private FunctionVisitor? _functionVisitor;

            private Queue<(ISymbol Symbol, AccessorKind AccessorKind, SyntaxNode DeclarationSyntax, SyntaxNode? BodySyntax)>? _deferred;

            public Visitor(SemanticModelAnalysisContext semanticModelContext, Context context)
            {
                SemanticModelContext = semanticModelContext;
                Context = context;
                _accessScopes = new Stack<Access>();
                _catchTypesScopes = new Stack<(ExceptionTypesBuilder?, ExceptionTypesBuilder?)>();
                _rethrowTypesScopes = new Stack<ExceptionTypesBuilder>();
                _builder = new ExceptionTypesBuilder();
            }

            protected SemanticModel SemanticModel => SemanticModelContext.SemanticModel;

            protected Compilation Compilation => Context.Compilation;

            protected CancellationToken CancellationToken => SemanticModelContext.CancellationToken;

            protected void Visit(SyntaxNode? node, Access access)
            {
                _accessScopes.Push(access);
                try
                {
                    Visit(node);
                }
                finally
                {
                    _ = _accessScopes.Pop();
                }
            }

            public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
            {
                // Don't analyze anonymous method as part of method body.

                VisitAnonymousFunctionExpression(node, node.DelegateKeyword.Span);
            }

            public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
            {
                var access = node.Kind() switch
                {
                    SyntaxKind.SimpleAssignmentExpression => Access.Set,
                    SyntaxKind.AddAssignmentExpression => Access.GetAndSetOrAdd,
                    SyntaxKind.SubtractAssignmentExpression => Access.GetAndSetOrRemove,
                    _ => Access.GetAndSet,
                };

                Visit(node.Left, access);

                Visit(node.Right, Access.Get);
            }

            public override void VisitBinaryExpression(BinaryExpressionSyntax node)
            {
                Visit(node.Left);

                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol != null)
                {
                    Debug.Assert(symbol.Kind == SymbolKind.Method, $"Analyzing binary expression but symbol kind is {symbol.Kind}.");
                    Debug.Assert(((IMethodSymbol)symbol).MethodKind == MethodKind.BuiltinOperator ||
                                 ((IMethodSymbol)symbol).MethodKind == MethodKind.UserDefinedOperator, $"Analyzing binary expression but method symbol kind was {((IMethodSymbol)symbol).MethodKind}.");

                    var span = node.OperatorToken.Span;
                    HandleThrownExceptionTypes(span, symbol, AccessorKinds.Unspecified);
                }

                Visit(node.Right);
            }

            public override void VisitCastExpression(CastExpressionSyntax node)
            {
                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol != null)
                {
                    Debug.Assert(symbol.Kind == SymbolKind.Method, $"Analyzing cast expression but symbol kind is {symbol.Kind}.");
                    Debug.Assert(((IMethodSymbol)symbol).MethodKind == MethodKind.Conversion, $"Analyzing cast expression but method symbol kind was {((IMethodSymbol)symbol).MethodKind}.");

                    var span = TextSpan.FromBounds(node.OpenParenToken.SpanStart, node.CloseParenToken.Span.End);
                    HandleThrownExceptionTypes(span, symbol, AccessorKinds.Unspecified);
                }

                Visit(node.Expression);
            }

            public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
            {
                Visit(node.Condition, Access.Get);

                Visit(node.WhenTrue);

                Visit(node.WhenFalse);
            }

            public override void VisitConstructorInitializer(ConstructorInitializerSyntax node)
            {
                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol != null)
                {
                    Debug.Assert(symbol.Kind == SymbolKind.Method, $"Analyzing constructor initializer but symbol kind is {symbol.Kind}.");
                    Debug.Assert(((IMethodSymbol)symbol).MethodKind == MethodKind.Constructor, $"Analyzing constructor initializer but method symbol kind was {((IMethodSymbol)symbol).MethodKind}.");

                    var span = node.ThisOrBaseKeyword.Span;
                    HandleThrownExceptionTypes(span, symbol, AccessorKinds.Unspecified);
                }

                Visit(node.ArgumentList, Access.Get);
            }

            public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
            {
                Visit(node.Expression, Access.Get);

                // `GetSymbolInfo(...)` may return an undesired non-property (i.e., non-indexer)
                // symbol when `node.Expression` is an array.
                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol != null && symbol.Kind == SymbolKind.Property)
                {
                    var span = node.ArgumentList.OpenBracketToken.Span;
                    HandleThrownExceptionTypes(span, symbol, null);
                }

                Visit(node.ArgumentList, Access.Get);
            }

            public override void VisitGenericName(GenericNameSyntax node)
            {
                VisitSimpleName(node);
            }

            public override void VisitIdentifierName(IdentifierNameSyntax node)
            {
                VisitSimpleName(node);
            }

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                // Ignore "nameof(...)".
                if (node.Expression is IdentifierNameSyntax identifierNameSyntax &&
                    identifierNameSyntax.Identifier.Kind() == SyntaxKind.NameOfKeyword)
                {
                    return;
                }

                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol != null)
                {
                    if (symbol.Kind != SymbolKind.Method)
                    {
                        Debug.Assert(false, $"Analyzing invocation expression but symbol kind is {symbol.Kind}.");
                        return;
                    }

                    var methodSymbol = (IMethodSymbol)symbol;
                    if (methodSymbol.MethodKind == MethodKind.DelegateInvoke)
                    {
                        Visit(node.Expression, Access.Get);

                        var span = node.ArgumentList.OpenParenToken.Span;
                        var delegateSymbol = methodSymbol.ContainingType;
                        HandleThrownExceptionTypes(span, delegateSymbol, AccessorKinds.Unspecified);
                    }
                    else
                    {
                        Visit(node.Expression, Access.Invoke);
                    }
                }

                Visit(node.ArgumentList, Access.Get);
            }

            public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
            {
                var symbol = SemanticModel.GetDeclaredSymbol(node, CancellationToken);
                if (symbol != null)
                {
                    var bodySyntax = (SyntaxNode)node.Body ?? node.ExpressionBody;

                    EnqueueDeferred(symbol, AccessorKind.Unspecified, node, bodySyntax);
                }

                // Do not analyze local function as part of the member body.
            }

            public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            {
                Visit(node.Expression, Access.Get);

                Visit(node.Name);
            }

            public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
            {
                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol != null)
                {
                    Debug.Assert(symbol.Kind == SymbolKind.Method, $"Analyzing object creation expression but symbol kind is {symbol.Kind}.");
                    Debug.Assert(((IMethodSymbol)symbol).MethodKind == MethodKind.Constructor, $"Analyzing object creation expression but method symbol kind was {((IMethodSymbol)symbol).MethodKind}.");

                    var span = node.Type.Span;
                    HandleThrownExceptionTypes(span, symbol, AccessorKinds.Unspecified);
                }

                Visit(node.ArgumentList, Access.Get);

                Visit(node.Initializer, Access.Get);
            }

            public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
            {
                // Don't analyze lambda as part of method body.

                VisitAnonymousFunctionExpression(node, node.ArrowToken.Span);
            }

            public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
            {
                switch (node.Kind())
                {
                    case SyntaxKind.PostDecrementExpression:
                    case SyntaxKind.PostIncrementExpression:
                        Visit(node.Operand, Access.GetAndSet);
                        break;

                    case SyntaxKind.SuppressNullableWarningExpression:
                        Visit(node.Operand);
                        return;

                    default:
                        Visit(node.Operand);
                        break;
                }

                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol != null)
                {
                    Debug.Assert(symbol.Kind == SymbolKind.Method, $"Analyzing postfix unary expression but symbol kind is {symbol.Kind}.");
                    Debug.Assert(((IMethodSymbol)symbol).MethodKind == MethodKind.BuiltinOperator ||
                                 ((IMethodSymbol)symbol).MethodKind == MethodKind.UserDefinedOperator, $"Analyzing postfix unary expression but method symbol kind was {((IMethodSymbol)symbol).MethodKind}.");

                    var span = node.OperatorToken.Span;
                    HandleThrownExceptionTypes(span, symbol, AccessorKinds.Unspecified);
                }
            }

            public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
            {
                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol != null)
                {
                    Debug.Assert(symbol.Kind == SymbolKind.Method, $"Analyzing prefix unary expression but symbol kind is {symbol.Kind}.");
                    Debug.Assert(((IMethodSymbol)symbol).MethodKind == MethodKind.BuiltinOperator ||
                                 ((IMethodSymbol)symbol).MethodKind == MethodKind.UserDefinedOperator, $"Analyzing prefix unary expression but method symbol kind was {((IMethodSymbol)symbol).MethodKind}.");

                    var span = node.OperatorToken.Span;
                    HandleThrownExceptionTypes(span, symbol, AccessorKinds.Unspecified);
                }

                switch (node.Kind())
                {
                    case SyntaxKind.PreDecrementExpression:
                    case SyntaxKind.PreIncrementExpression:
                        Visit(node.Operand, Access.GetAndSet);
                        break;

                    default:
                        Visit(node.Operand);
                        break;
                }
            }

            public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
            {
                // Don't analyze lambda as part of method body.

                VisitAnonymousFunctionExpression(node, node.ArrowToken.Span);
            }

            public override void VisitThrowExpression(ThrowExpressionSyntax node)
            {
                base.VisitThrowExpression(node);

                var thrownTypeInfo = SemanticModel.GetTypeInfo(node.Expression, CancellationToken);
                if (thrownTypeInfo.Type is INamedTypeSymbol thrownType && thrownType.TypeKind != TypeKind.Error)
                    if (!TryIgnoreOrAddCaughtExceptionType(thrownType, thrownType.OriginalDefinition))
                        HandleUncaughtExceptionType(node.Span, null, default, thrownType);
            }

            public override void VisitThrowStatement(ThrowStatementSyntax node)
            {
                base.VisitThrowStatement(node);

                if (node.Expression != null)
                {
                    var thrownTypeInfo = SemanticModel.GetTypeInfo(node.Expression, CancellationToken);
                    if (thrownTypeInfo.Type is INamedTypeSymbol thrownType && thrownType.TypeKind != TypeKind.Error)
                        if (!TryIgnoreOrAddCaughtExceptionType(thrownType, thrownType.OriginalDefinition))
                            HandleUncaughtExceptionType(node.Span, null, default, thrownType);
                }
                else
                {
                    if (_rethrowTypesScopes.Count > 0)
                    {
                        var rethrowTypes = _rethrowTypesScopes.Peek();

                        foreach (var rethrownType in rethrowTypes)
                            if (!TryIgnoreOrAddCaughtExceptionType(rethrownType, rethrownType.OriginalDefinition))
                                _builder.Add(rethrownType);

                        if (_builder.Count > 0)
                        {
                            var uncaughtTypes = _builder.ToImmutable();
                            _builder.Clear();

                            HandleUncaughtExceptionTypes(node.Span, null, default, uncaughtTypes);
                        }
                    }
                }
            }

            public override void VisitTryStatement(TryStatementSyntax node)
            {
                ExceptionTypesBuilder? catchTypes = null;
                ExceptionTypesBuilder? generalCatchRethrowTypes = null;

                // Determine the exception types that will be caught from the "try" block.
                foreach (var catchClause in node.Catches)
                {
                    var declaration = catchClause.Declaration;
                    if (declaration != null)
                    {
                        if (catchTypes == null)
                            catchTypes = ExceptionTypesBuilder.Allocate();

                        var catchTypeInfo = SemanticModel.GetTypeInfo(declaration.Type, CancellationToken);
                        if (catchTypeInfo.Type is INamedTypeSymbol catchType)
                        {
                            // If there is no filter then the declared exception type will be
                            // caught; otherwise it may not be caught.
                            if (catchClause.Filter == null)
                            {
                                catchTypes.Add(catchType);
                            }
                            else if (declaration.Identifier != null)
                            {
                                // The filter may define a set of exception types that
                                // will be caught.
                                _ = AddFilterTypesFromIsExpressions(catchType, catchClause, catchTypes);
                            }
                        }
                    }
                    else
                    {
                        // If there is no filter then exceptions will be caught; otherwise they may
                        // not be caught.
                        if (catchClause.Filter == null ||
                            EvaluatesToTrue(catchClause.Filter.FilterExpression))
                        {
                            if (generalCatchRethrowTypes == null)
                                generalCatchRethrowTypes = ExceptionTypesBuilder.Allocate();
                        }
                        else
                        {
                            if (catchTypes == null)
                                catchTypes = ExceptionTypesBuilder.Allocate();
                        }
                    }
                }

                _catchTypesScopes.Push((catchTypes, generalCatchRethrowTypes));
                try
                {
                    Visit(node.Block);
                }
                finally
                {
                    _ = _catchTypesScopes.Pop();
                }

                foreach (var catchClause in node.Catches)
                {
                    Visit(catchClause.Filter);

                    ExceptionTypesBuilder rethrowTypes;

                    // Determine the exception types that were caught from the "try" block and may
                    // be rethrown in the "catch" block.
                    var declaration = catchClause.Declaration;
                    if (declaration != null)
                    {
                        catchTypes!.Clear();

                        var catchTypeInfo = SemanticModel.GetTypeInfo(declaration.Type, CancellationToken);
                        if (catchTypeInfo.Type is INamedTypeSymbol catchType)
                        {
                            if (catchClause.Filter == null ||
                                declaration.Identifier == null)
                            {
                                catchTypes.Add(catchType);
                            }
                            else
                            {
                                // If the filter _only_ defines a set of exception types that will
                                // be caught then only rethrow those types; otherwise, rethrow those
                                // types and also the declared exception type.
                                if (!AddFilterTypesFromIsExpressions(catchType, catchClause, catchTypes))
                                    catchTypes.Add(catchType);
                            }
                        }

                        rethrowTypes = catchTypes;
                    }
                    else
                    {
                        // If there is no filter then otherwise-uncaught exception types caught from
                        // the "try" block may be rethrown; otherwise those exception types were
                        // considered uncaught and do not need to be reported again.
                        if (generalCatchRethrowTypes != null)
                        {
                            rethrowTypes = generalCatchRethrowTypes;
                        }
                        else
                        {
                            catchTypes!.Clear();

                            rethrowTypes = catchTypes;
                        }
                    }

                    _rethrowTypesScopes.Push(rethrowTypes);
                    try
                    {
                        Visit(catchClause);
                    }
                    finally
                    {
                        _ = _rethrowTypesScopes.Pop();
                    }
                }

                catchTypes?.Free();
                generalCatchRethrowTypes?.Free();

                Visit(node.Finally);
            }

            protected abstract void HandleDelegateCreation(TextSpan span, IMethodSymbol symbol, ITypeSymbol delegateType);

            protected virtual void HandleThrownExceptionTypes(TextSpan span, ISymbol throwerSymbol, AccessorKinds throwerAccessorKinds)
            {
                var thrownExceptionTypes = GetThrownExceptionTypes(throwerSymbol);

                HandleThrownExceptionTypes(span, throwerSymbol, throwerAccessorKinds, thrownExceptionTypes);
            }

            /// <summary>
            /// Handle exception types thrown by a specified accessors of a symbol, given an array
            /// of documented exception types for that symbol.
            /// </summary>
            protected void HandleThrownExceptionTypes(TextSpan span, ISymbol throwerSymbol, AccessorKinds throwerAccessorKinds, ImmutableArray<DocumentedExceptionType> thrownExceptionTypes)
            {
                var enumerator = throwerAccessorKinds.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var accessorKind = enumerator.Current;

                    foreach (var thrownExceptionType in thrownExceptionTypes)
                    {
                        if (thrownExceptionType.AccessorKind != accessorKind)
                            continue;

                        var originalExceptionType = thrownExceptionType.ExceptionType.OriginalDefinition;
                        if (!originalExceptionType.HasBaseConversionTo(Context.IntransitiveExceptionTypes) || IsThrowHelper(throwerSymbol, thrownExceptionType.ExceptionType))
                            if (!TryIgnoreOrAddCaughtExceptionType(thrownExceptionType.ExceptionType, originalExceptionType))
                                _builder.Add(thrownExceptionType.ExceptionType);
                    }

                    if (_builder.Count > 0)
                    {
                        var uncaughtTypes = _builder.ToImmutable();
                        _builder.Clear();

                        HandleUncaughtExceptionTypes(span, throwerSymbol, accessorKind, uncaughtTypes);
                    }
                }
            }

            /// <summary>
            /// Indicates whether an uncaught exception type should be ignored.
            /// </summary>
            protected abstract bool IsIgnoredExceptionType(INamedTypeSymbol exceptionType);

            /// <summary>
            /// Handles a specified uncaught exception type.
            /// </summary>
            protected virtual void HandleUncaughtExceptionType(TextSpan span, ISymbol? throwerSymbol, AccessorKind throwerAccessorKind, INamedTypeSymbol exceptionType)
            {
                HandleUncaughtExceptionTypes(span, throwerSymbol, throwerAccessorKind, ImmutableArray.Create(exceptionType));
            }

            /// <summary>
            /// Handles specified uncaught exception types.
            /// </summary>
            protected abstract void HandleUncaughtExceptionTypes(TextSpan span, ISymbol? throwerSymbol, AccessorKind throwerAccessorKind, ImmutableArray<INamedTypeSymbol> exceptionTypes);

            protected ImmutableArray<DocumentedExceptionType> GetThrownExceptionTypes(ISymbol symbol)
            {
                ImmutableArray<DocumentedExceptionType> thrownExceptionTypes;

                if (TryGetDocumentedExceptionTypes(symbol, out var documentedExceptionTypes))
                    return documentedExceptionTypes;

                if (symbol.Kind == SymbolKind.Method)
                {
                    var methodSymbol = (IMethodSymbol)symbol;
                    if (methodSymbol.MethodKind == MethodKind.LocalFunction)
                    {
                        if (_functionVisitor == null)
                            _functionVisitor = new FunctionVisitor(SemanticModelContext, Context);

                        foreach (var syntaxReference in methodSymbol.DeclaringSyntaxReferences)
                        {
                            var localFunctionSyntax = (LocalFunctionStatementSyntax)syntaxReference.GetSyntax(CancellationToken);

                            var bodySyntax = (SyntaxNode)localFunctionSyntax.Body ?? localFunctionSyntax.ExpressionBody;

                            _functionVisitor.Analyze(methodSymbol, localFunctionSyntax, bodySyntax);
                        }

                        thrownExceptionTypes = _functionVisitor.ThrownExceptionTypesBuilder.ToImmutable();
                        _functionVisitor.ThrownExceptionTypesBuilder.Clear();

                        return thrownExceptionTypes;
                    }
                    else if (methodSymbol.MethodKind == MethodKind.AnonymousFunction)
                    {
                        if (_functionVisitor == null)
                            _functionVisitor = new FunctionVisitor(SemanticModelContext, Context);

                        foreach (var syntaxReference in methodSymbol.DeclaringSyntaxReferences)
                        {
                            var anonymousFunctionSyntax = (AnonymousFunctionExpressionSyntax)syntaxReference.GetSyntax(CancellationToken);

                            var bodySyntax = anonymousFunctionSyntax.Body;

                            _functionVisitor.Analyze(methodSymbol, anonymousFunctionSyntax, bodySyntax);
                        }

                        thrownExceptionTypes = _functionVisitor.ThrownExceptionTypesBuilder.ToImmutable();
                        _functionVisitor.ThrownExceptionTypesBuilder.Clear();

                        return thrownExceptionTypes;
                    }
                }

                return ImmutableArray<DocumentedExceptionType>.Empty;
            }

            protected ImmutableArray<DocumentedExceptionType> GetDocumentedExceptionTypes(ISymbol symbol)
            {
                return Context.DocumentedExceptionTypesProvider.GetDocumentedExceptionTypes(symbol, CancellationToken);
            }

            protected bool TryGetDocumentedExceptionTypes(ISymbol symbol, out ImmutableArray<DocumentedExceptionType> documentedExceptionTypes)
            {
                return Context.DocumentedExceptionTypesProvider.TryGetDocumentedExceptionTypes(symbol, out documentedExceptionTypes, CancellationToken);
            }

            protected internal void EnqueueDeferred(ISymbol symbol, AccessorKind accessorKind, SyntaxNode declarationSyntax, SyntaxNode? bodySyntax)
            {
                if (_deferred is null)
                    _deferred = new Queue<(ISymbol, AccessorKind, SyntaxNode, SyntaxNode?)>();

                _deferred.Enqueue((symbol, accessorKind, declarationSyntax, bodySyntax));
            }

            protected bool TryDequeDeferred([NotNullWhen(true)] out ISymbol? symbol, out AccessorKind accessorKind, [NotNullWhen(true)] out SyntaxNode? declarationSyntax, out SyntaxNode? bodySyntax)
            {
                if (_deferred is null || _deferred.Count == 0)
                {
                    symbol = default;
                    accessorKind = default;
                    declarationSyntax = default;
                    bodySyntax = default;
                    return false;
                }

                (symbol, accessorKind, declarationSyntax, bodySyntax) = _deferred.Dequeue();
                return true;
            }

            protected ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> GetAdjustmentsFromComments(SyntaxNode? node)
            {
                if (node == null)
                    return ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>>.Empty;

                var result = ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>>.Empty;

                const string exceptionAdjustmentPrefix = "// ExceptionAdjustment: ";

                var leadingTrivia = node.GetLeadingTrivia();

                foreach (var trivia in leadingTrivia)
                {
                    if (trivia.Kind() == SyntaxKind.SingleLineCommentTrivia)
                    {
                        string line = trivia.ToString();
                        if (line.StartsWith(exceptionAdjustmentPrefix, StringComparison.Ordinal))
                        {
                            line = line.Substring(exceptionAdjustmentPrefix.Length);
                            var span = TextSpan.FromBounds(trivia.Span.Start + exceptionAdjustmentPrefix.Length, trivia.Span.End);

                            if (ExceptionAdjustmentsFile.TryParseAdjustment(line, span, ReportDiagnostic, out string? symbolId, out var adjustment))
                            {
                                var symbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(symbolId, Compilation);
                                if (symbol is null)
                                {
                                    SemanticModelContext.ReportDiagnostic(Diagnostic.Create(
                                        descriptor: ExceptionAdjustmentsFileAnalyzer.SymbolRule,
                                        location: Location.Create(node.SyntaxTree, adjustment.SymbolIdSpan)));
                                }

                                var exceptionType = DocumentationCommentId.GetFirstSymbolForDeclarationId(adjustment.ExceptionTypeId, Compilation);
                                if (exceptionType is null)
                                {
                                    SemanticModelContext.ReportDiagnostic(Diagnostic.Create(
                                        descriptor: ExceptionAdjustmentsFileAnalyzer.SymbolRule,
                                        location: Location.Create(node.SyntaxTree, adjustment.ExceptionTypeIdSpan)));
                                }

                                ImmutableArray<MemberExceptionAdjustment> adjustments;
                                adjustments = result.TryGetValue(symbolId, out adjustments)
                                    ? adjustments.Add(adjustment)
                                    : ImmutableArray.Create(adjustment);
                                result = result.SetItem(symbolId, adjustments);
                            }
                        }
                    }
                }

                return result;

                void ReportDiagnostic(DiagnosticDescriptor descriptor, TextSpan span)
                {
                    SemanticModelContext.ReportDiagnostic(Diagnostic.Create(
                        descriptor: descriptor,
                        Location.Create(node.SyntaxTree, span)));
                }
            }

            private bool TryGetCreatedDelegateType(ExpressionSyntax node, out ITypeSymbol delegateType)
            {
                delegateType = SemanticModel.GetTypeInfo(node, CancellationToken).ConvertedType;
                if (delegateType == null)
                    return false;

                // Unwrap `Expression<TDelegate>`.
                if (delegateType.TypeKind == TypeKind.Class &&
                    delegateType is INamedTypeSymbol namedDelegateSymbol)
                {
                    var expressionType = Compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression`1");
                    if (SymbolEqualityComparer.Default.Equals(delegateType.OriginalDefinition, expressionType))
                        delegateType = namedDelegateSymbol.TypeArguments[0];
                }

                if (delegateType.TypeKind == TypeKind.Error)
                    return false;

                Debug.Assert(delegateType.TypeKind == TypeKind.Delegate, $"Expected delegate but type kind is {delegateType.TypeKind}.");

                return true;
            }

            private bool AddFilterTypesFromIsExpressions(INamedTypeSymbol catchType, CatchClauseSyntax catchClause, ExceptionTypesBuilder catchTypes)
            {
                var catchSymbol = SemanticModel.GetDeclaredSymbol(catchClause.Declaration, CancellationToken);

                var expression = catchClause.Filter.FilterExpression;

                int index = catchTypes.Count;

                bool complete = AddTypesFromIsExpressions(catchType, catchSymbol, expression, catchTypes);

                catchTypes.Reverse(index);

                return complete;
            }

            /// <summary>
            /// Adds the types from logical-ORed <c>is</c> expressions on <paramref name="symbol"/>.
            /// Returns <see langword="true"/> if the type of <paramref name="symbol"/> must be one
            /// of the types added to <paramref name="types"/> in order to satisfy
            /// <paramref name="expression"/> (and thus the type of <paramref name="symbol"/> can be
            /// treated as more refined than its declared type).
            /// </summary>
            private bool AddTypesFromIsExpressions(INamedTypeSymbol catchType, ISymbol symbol, ExpressionSyntax expression, ExceptionTypesBuilder types)
            {
                bool complete = true;

                while (expression.Kind() == SyntaxKind.ParenthesizedExpression)
                    expression = ((ParenthesizedExpressionSyntax)expression).Expression;

                while (expression.Kind() == SyntaxKind.LogicalOrExpression)
                {
                    var orExpression = (BinaryExpressionSyntax)expression;

                    complete &= AddTypesFromIsExpressions(catchType, symbol, orExpression.Right, types);

                    expression = orExpression.Left;

                    while (expression.Kind() == SyntaxKind.ParenthesizedExpression)
                        expression = ((ParenthesizedExpressionSyntax)expression).Expression;
                }

                if (expression.Kind() == SyntaxKind.IsExpression)
                {
                    var isExpression = (BinaryExpressionSyntax)expression;
                    if (isExpression.Left.Kind() == SyntaxKind.IdentifierName &&
                        isExpression.Right is TypeSyntax)
                    {
                        var isIdentifierName = (IdentifierNameSyntax)isExpression.Left;
                        var isSymbol = SemanticModel.GetSymbolInfo(isIdentifierName, CancellationToken).Symbol;

                        if (SymbolEqualityComparer.Default.Equals(symbol, isSymbol))
                        {
                            var isTypeInfo = SemanticModel.GetTypeInfo(isExpression.Right, CancellationToken);
                            if (isTypeInfo.Type is INamedTypeSymbol isType)
                            {
                                if (isType.HasBaseConversionTo(catchType))
                                    types.Add(isType);

                                return complete;
                            }
                        }
                    }
                }

                return false;
            }

            /// <summary>
            /// Performs simple evaluation of logical-OR expressions.
            /// </summary>
            private bool EvaluatesToTrue(ExpressionSyntax expression)
            {
                while (expression.Kind() == SyntaxKind.ParenthesizedExpression)
                    expression = ((ParenthesizedExpressionSyntax)expression).Expression;

                while (expression.Kind() == SyntaxKind.LogicalOrExpression)
                {
                    var orExpression = (BinaryExpressionSyntax)expression;

                    if (EvaluatesToTrue(orExpression.Right))
                        return true;

                    expression = orExpression.Left;

                    while (expression.Kind() == SyntaxKind.ParenthesizedExpression)
                        expression = ((ParenthesizedExpressionSyntax)expression).Expression;
                }

                return expression.Kind() == SyntaxKind.TrueLiteralExpression;
            }

            private void VisitAnonymousFunctionExpression(AnonymousFunctionExpressionSyntax node, TextSpan span)
            {
                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol != null)
                {
                    Debug.Assert(symbol.Kind == SymbolKind.Method, $"Analyzing anonymous function expression but symbol kind is {symbol.Kind}.");
                    Debug.Assert(((IMethodSymbol)symbol).MethodKind == MethodKind.AnonymousFunction, $"Analyzing anonymous function expression but method symbol kind was {((IMethodSymbol)symbol).MethodKind}.");

                    var bodySyntax = node.Body;

                    EnqueueDeferred(symbol, AccessorKind.Unspecified, node, bodySyntax);

                    if (TryGetCreatedDelegateType(node, out var delegateSymbol))
                        HandleDelegateCreation(span, (IMethodSymbol)symbol, delegateSymbol);
                }
            }

            private void VisitSimpleName(SimpleNameSyntax node)
            {
                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol == null)
                    return;

                HandleThrownExceptionTypes(node.Span, symbol, node);
            }

            /// <summary>
            /// Handles exception types thrown by a specified symbol.  The accessors of the symbol
            /// are determined by the context of the node being visited in the syntax tree.
            /// </summary>
            private void HandleThrownExceptionTypes(TextSpan span, ISymbol symbol, ExpressionSyntax? node)
            {
                AccessorKinds accessorKinds;
                switch (symbol.Kind)
                {
                    case SymbolKind.Event:
                        switch (_accessScopes.Peek())
                        {
                            case Access.Get:
                                return;
                            case Access.GetAndSetOrAdd:
                                accessorKinds = AccessorKinds.Add;
                                break;
                            case Access.GetAndSetOrRemove:
                                accessorKinds = AccessorKinds.Remove;
                                break;
                            case Access.Invoke:
                                return;
                            default:
#if DEBUG
                                Debug.Assert(false, $"Analyzing event symbol but access is {_accessScopes.Peek()}.");
#endif
                                return;
                        }
                        break;

                    case SymbolKind.Method:
                        switch (_accessScopes.Peek())
                        {
                            case Access.Get:
                                if (TryGetCreatedDelegateType(node!, out var delegateType))
                                    HandleDelegateCreation(node!.Span, (IMethodSymbol)symbol, delegateType);
                                return;
                            case Access.Invoke:
                                accessorKinds = AccessorKinds.Unspecified;
                                break;
                            default:
#if DEBUG
                                Debug.Assert(false, $"Analyzing method symbol but access is {_accessScopes.Peek()}.");
#endif
                                return;
                        }
                        break;

                    case SymbolKind.Property:
                        switch (_accessScopes.Peek())
                        {
                            case Access.Get:
                                accessorKinds = AccessorKinds.Get;
                                break;
                            case Access.Set:
                                accessorKinds = AccessorKinds.Set;
                                break;
                            case Access.GetAndSet:
                            case Access.GetAndSetOrAdd:
                            case Access.GetAndSetOrRemove:
                                accessorKinds = AccessorKinds.Get | AccessorKinds.Set;
                                break;
                            default:
#if DEBUG
                                Debug.Assert(false, $"Analyzing property symbol but access is {_accessScopes.Peek()}.");
#endif
                                return;
                        }

                        var propertySymbol = (IPropertySymbol)symbol;
                        if (propertySymbol.SetMethod == null)
                        {
                            // "ref"-returning get accessor.
                            accessorKinds = AccessorKinds.Get;
                        }
                        break;

                    default:
                        return;
                }

                HandleThrownExceptionTypes(span, symbol, accessorKinds);
            }

            /// <summary>
            /// Indicates whether a symbol is a throw helper.
            /// </summary>
            private bool IsThrowHelper(ISymbol symbol, INamedTypeSymbol exceptionType)
            {
                if (symbol.Kind != SymbolKind.Method)
                    return false;

                bool result = IsThrowHelperName(symbol.Name);

                string? exceptionTypeId = exceptionType.GetDeclarationDocumentationCommentId();

                if (Context.DocumentedExceptionTypesProvider.TryGetExceptionAdjustments(symbol, out var adjustments, CancellationToken))
                {
                    const string throwerFlag = "thrower";

                    if (result)
                    {
                        foreach (var adjustment in adjustments)
                        {
                            if (adjustment.Kind == ExceptionAdjustmentKind.Removal &&
                                adjustment.Accessor == null &&
                                adjustment.Flag == throwerFlag &&
                                adjustment.ExceptionTypeId == exceptionTypeId)
                            {
                                result = false;
                                break;
                            }
                        }
                    }

                    if (!result)
                    {
                        foreach (var adjustment in adjustments)
                        {
                            if (adjustment.Kind == ExceptionAdjustmentKind.Addition &&
                                adjustment.Accessor == null &&
                                adjustment.Flag == throwerFlag &&
                                adjustment.ExceptionTypeId == exceptionTypeId)
                            {
                                result = true;
                                break;
                            }
                        }
                    }
                }

                return result;

                static bool IsThrowHelperName(string name)
                {
                    const string throwHelperName = "Throw";

                    if (name.Length >= 5 && name.StartsWith(throwHelperName, StringComparison.Ordinal))
                    {
                        if (name.Length == 5)
                            return true;
                        char c = name[throwHelperName.Length];
                        return c < 'a' || c > 'z';
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            /// <summary>
            /// Indicates whether an exception type is ignored/caught.  If the exception type is
            /// caught by a general catch clause, the type is added to the list of exception types
            /// caught by that clause.
            /// </summary>
            private bool TryIgnoreOrAddCaughtExceptionType(INamedTypeSymbol exceptionType, INamedTypeSymbol originalExceptionType)
            {
                if (originalExceptionType.HasBaseConversionTo(Context.IgnoredExceptionTypes))
                    return true;

                // Look for enclosing "catch" clause.
                foreach (var (catchTypes, generalCatchRethrowTypes) in _catchTypesScopes)
                {
                    if (catchTypes != null)
                        foreach (var catchType in catchTypes)
                            if (exceptionType.HasBaseConversionTo(catchType))
                                return true;

                    if (generalCatchRethrowTypes != null)
                    {
                        generalCatchRethrowTypes.Add(exceptionType);
                        return true;
                    }
                }

                return IsIgnoredExceptionType(exceptionType);
            }

            protected enum Access
            {
                None,
                Get,
                Set,
                GetAndSet,
                GetAndSetOrAdd,
                GetAndSetOrRemove,
                Invoke,
            }
        }
    }
}
