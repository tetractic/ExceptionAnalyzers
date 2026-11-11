// Copyright Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
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

            private static readonly TryScopePool _tryScopePool = new TryScopePool(Environment.ProcessorCount, 32);

            private readonly Stack<Access> _accessScopes;

            /// <summary>
            /// Information about exception types that may be thrown so far in each enclosing "try"
            /// block.
            /// </summary>
            /// <remarks>
            /// Information about exception types is added to the top list if the stack is not
            /// empty.  The exception types are reported after analyzing the "try" block if not
            /// caught or if caught and rethrown.
            /// </remarks>
            private readonly Stack<List<TryItem>> _tryScopes;

            /// <summary>
            /// Information about each enclosing "catch" clause, which is used when analyzing a
            /// rethrow.
            /// </summary>
            private readonly Stack<CatchScope> _catchScopes;

            private readonly ExceptionTypesBuilder _builder;

            private FunctionVisitor? _functionVisitor;

            private FilterEvaluator? _filterEvaluator;

            private Queue<(ISymbol Symbol, AccessorKind AccessorKind, SyntaxNode DeclarationSyntax, SyntaxNode? BodySyntax)>? _deferred;

            public Visitor(SemanticModelAnalysisContext semanticModelContext, Context context)
            {
                SemanticModelContext = semanticModelContext;
                Context = context;
                _accessScopes = new Stack<Access>();
                _tryScopes = new Stack<List<TryItem>>();
                _catchScopes = new Stack<CatchScope>();
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
                var access = node.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    ? Access.Set
                    : Access.GetAndSet;

                Visit(node.Left, access);

                // Ignore operators that cannot be user-defined.
                if (!node.OperatorToken.IsKind(SyntaxKind.QuestionQuestionEqualsToken))
                {
                    var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                    if (symbol != null)
                    {
                        if (symbol.Kind == SymbolKind.Method)
                        {
                            var methodSymbol = (IMethodSymbol)symbol;
                            switch (methodSymbol.MethodKind)
                            {
                                case MethodKind.EventAdd:
                                case MethodKind.EventRemove:
                                {
                                    var accessorKinds = methodSymbol.MethodKind switch
                                    {
                                        MethodKind.EventAdd => AccessorKinds.Add,
                                        MethodKind.EventRemove => AccessorKinds.Remove,
                                        _ => throw new UnreachableException(),
                                    };

                                    symbol = SemanticModel.GetSymbolInfo(node.Left, CancellationToken).Symbol;
                                    if (symbol != null)
                                    {
                                        Debug.Assert(symbol.Kind == SymbolKind.Event, $"Analyzing event add/remove accessor but symbol kind was {symbol.Kind}.");

                                        var span = node.OperatorToken.Span;
                                        HandleThrownExceptionTypes(span, symbol, accessorKinds);
                                    }
                                    break;
                                }

                                default:
                                {
                                    Debug.Assert(methodSymbol.MethodKind == MethodKind.BuiltinOperator ||
                                                 methodSymbol.MethodKind == MethodKind.UserDefinedOperator, $"Analyzing binary expression but method symbol kind was {((IMethodSymbol)symbol).MethodKind}.");

                                    var span = node.OperatorToken.Span;
                                    HandleThrownExceptionTypes(span, symbol, AccessorKinds.Unspecified);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            Debug.Fail($"Analyzing binary expression but symbol kind is {symbol.Kind}.");
                        }
                    }
                }

                Visit(node.Right, Access.Get);
            }

            public override void VisitBinaryExpression(BinaryExpressionSyntax node)
            {
                Visit(node.Left);

                // Ignore operators that cannot be user-defined.
                if (!node.OperatorToken.IsKind(SyntaxKind.QuestionQuestionToken))
                {
                    var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                    if (symbol != null)
                    {
                        Debug.Assert(symbol.Kind == SymbolKind.Method, $"Analyzing binary expression but symbol kind is {symbol.Kind}.");
                        Debug.Assert(((IMethodSymbol)symbol).MethodKind == MethodKind.BuiltinOperator ||
                                     ((IMethodSymbol)symbol).MethodKind == MethodKind.UserDefinedOperator, $"Analyzing binary expression but method symbol kind was {((IMethodSymbol)symbol).MethodKind}.");

                        var span = node.OperatorToken.Span;
                        HandleThrownExceptionTypes(span, symbol, AccessorKinds.Unspecified);
                    }
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

            public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
            {
                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol != null)
                {
                    Debug.Assert(symbol.Kind == SymbolKind.Method, $"Analyzing implicit object creation expression but symbol kind is {symbol.Kind}.");
                    Debug.Assert(((IMethodSymbol)symbol).MethodKind == MethodKind.Constructor, $"Analyzing implicit object creation expression but method symbol kind was {((IMethodSymbol)symbol).MethodKind}.");

                    var span = node.NewKeyword.Span;
                    HandleThrownExceptionTypes(span, symbol, AccessorKinds.Unspecified);
                }

                Visit(node.ArgumentList, Access.Get);

                Visit(node.Initializer, Access.Get);
            }

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                // Ignore "nameof(...)".
                if (node.Expression is IdentifierNameSyntax identifierNameSyntax &&
                    identifierNameSyntax.Identifier.IsKind(SyntaxKind.NameOfKeyword))
                {
                    return;
                }

                var symbol = SemanticModel.GetSymbolInfo(node, CancellationToken).Symbol;
                if (symbol != null)
                {
                    if (symbol.Kind == SymbolKind.Method)
                    {
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
                            Debug.Assert(methodSymbol.MethodKind == MethodKind.Ordinary ||
                                         methodSymbol.MethodKind == MethodKind.ReducedExtension ||
                                         methodSymbol.MethodKind == MethodKind.LocalFunction, $"Analyzing invocation expression but method symbol kind was {((IMethodSymbol)symbol).MethodKind}.");

                            Visit(node.Expression, Access.Invoke);
                        }
                    }
                    else
                    {
                        Debug.Fail($"Analyzing invocation expression but symbol kind is {symbol.Kind}.");
                    }
                }

                Visit(node.ArgumentList, Access.Get);
            }

            public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
            {
                var symbol = SemanticModel.GetDeclaredSymbol(node, CancellationToken);
                if (symbol != null)
                {
                    var bodySyntax = (SyntaxNode?)node.Body ?? node.ExpressionBody;

                    EnqueueDeferred(symbol, AccessorKind.Unspecified, node, bodySyntax);
                }

                // Don't analyze local function as part of the member body.
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

                HandleThrowExceptionTypes(node.Span, node.Expression);
            }

            public override void VisitThrowStatement(ThrowStatementSyntax node)
            {
                base.VisitThrowStatement(node);

                if (node.Expression != null)
                    HandleThrowExceptionTypes(node.Span, node.Expression);
                else
                    HandleRethrowExceptionTypes();
            }

            public override void VisitTryStatement(TryStatementSyntax node)
            {
                var tryItems = _tryScopePool.Take();

                _tryScopes.Push(tryItems);
                try
                {
                    Visit(node.Block);
                }
                finally
                {
                    _ = _tryScopes.Pop();
                }

                foreach (var catchClause in node.Catches)
                {
                    INamedTypeSymbol? catchType;
                    ISymbol? catchSymbol;

                    var declaration = catchClause.Declaration;
                    if (declaration != null)
                    {
                        var catchTypeInfo = SemanticModel.GetTypeInfo(declaration.Type, CancellationToken);
                        if (catchTypeInfo.Type is INamedTypeSymbol type)
                        {
                            catchType = type;
                            catchSymbol = SemanticModel.GetDeclaredSymbol(declaration, CancellationToken);
                        }
                        else
                        {
                            catchType = Compilation.GetSpecialType(SpecialType.System_Void);
                            catchSymbol = null;
                        }
                    }
                    else
                    {
                        catchType = null;
                        catchSymbol = null;
                    }

                    var catchScope = new CatchScope(tryItems, catchType, catchSymbol, catchClause.Filter?.FilterExpression);

                    _catchScopes.Push(catchScope);
                    try
                    {
                        Visit(catchClause);
                    }
                    finally
                    {
                        _ = _catchScopes.Pop();
                    }

                    // If an exception type will be caught by the current "catch" clause, it is
                    // tracked as caught so that subsequent "catch" clauses can ignore it and so
                    // that it will not be handled as uncaught by the "try" statement unless
                    // rethrown.

                    bool? blindFilterResult = catchClause.Filter == null
                        ? true
                        : BlindFilterEvaluator.Instance.Visit(catchClause.Filter.FilterExpression);

                    // If everything passes the filter, an exception type will be caught if it is
                    // the "catch" type or a subtype of it.
                    if (blindFilterResult == true)
                    {
                        for (int i = 0; i < tryItems.Count; ++i)
                        {
                            var tryItem = tryItems[i];

                            foreach (var exceptionType in tryItem.Types)
                            {
                                // Skip types that have been caught by a prior "catch" clause or may
                                // not be caught by the current "catch" clause.
                                if (tryItem.Caught(exceptionType) || !catchScope.TypeWillCatch(exceptionType))
                                    continue;

                                EnsureCaughtTypes(tryItems, i, ref tryItem);

                                tryItem.CaughtTypes!.Add(exceptionType);
                            }
                        }
                    }
                    // If anything passes the filter and the filter might depend on the exception
                    // type, an exception type will be caught if it will pass the filter.
                    else if (blindFilterResult != false && catchSymbol != null)
                    {
                        var catchFilter = catchClause.Filter!.FilterExpression;

                        for (int i = 0; i < tryItems.Count; ++i)
                        {
                            var tryItem = tryItems[i];

                            foreach (var exceptionType in tryItem.Types)
                            {
                                // Skip types that have been caught by a prior "catch" clause or may
                                // not be caught by the current "catch" clause.
                                if (tryItem.Caught(exceptionType) || !catchScope.TypeWillCatch(exceptionType))
                                    continue;

                                _filterEvaluator ??= new FilterEvaluator(SemanticModel);

                                var result = _filterEvaluator.Evaluate(catchFilter, catchSymbol, exceptionType, rethrow: false, CancellationToken);
                                if (result.Result)
                                {
                                    EnsureCaughtTypes(tryItems, i, ref tryItem);

                                    tryItem.CaughtTypes!.Add(exceptionType);
                                }
                                else if (result.Subtypes != null)
                                {
                                    foreach (var subtype in result.Subtypes)
                                    {
                                        if (_filterEvaluator.EvaluateSubtype(catchFilter, catchSymbol, subtype, rethrow: false, CancellationToken))
                                        {
                                            EnsureCaughtTypes(tryItems, i, ref tryItem);

                                            tryItem.CaughtTypes!.Add(subtype);
                                        }
                                    }

                                    result.Subtypes.Free();
                                }
                            }
                        }
                    }
                }

                // Handle exception types that may be thrown in the "try" block and are not caught
                // or are caught and rethrown.
                foreach (var tryItem in tryItems)
                {
                    Debug.Assert(_builder.Count == 0);

                    var caughtTypes = tryItem.CaughtTypes;
                    var rethrownTypes = tryItem.RethrownTypes;

                    foreach (var exceptionType in tryItem.Types)
                    {
                        if (caughtTypes == null ||
                            !caughtTypes.Contains(exceptionType) ||
                            (rethrownTypes != null &&
                             rethrownTypes.Contains(exceptionType)))
                        {
                            _builder.Add(exceptionType);
                        }
                    }

                    // Subtypes can be rethrown without having been thrown.
                    if (tryItem.RethrownTypes != null)
                    {
                        foreach (var rethrownType in tryItem.RethrownTypes)
                        {
                            bool subsumed = false;
                            foreach (var uncaughtType in _builder)
                            {
                                if (rethrownType.HasBaseConversionTo(uncaughtType))
                                {
                                    subsumed = true;
                                    break;
                                }
                            }

                            if (!subsumed)
                                _builder.Add(rethrownType);
                        }
                    }

                    if (_builder.Count > 0)
                    {
                        var uncaughtTypes = _builder.ToImmutable();
                        _builder.Clear();

                        var span = tryItem.Span;
                        var throwerSymbol = tryItem.ThrowerSymbol;
                        var throwerAccessorKind = tryItem.ThrowerAccessorKind;

                        HandleExceptionTypes(span, throwerSymbol, throwerAccessorKind, uncaughtTypes);
                    }

                    tryItem.CaughtTypes?.Free();
                    tryItem.RethrownTypes?.Free();
                }

                _tryScopePool.Return(tryItems);

                Visit(node.Finally);

                static void EnsureCaughtTypes(List<TryItem> tryItems, int i, ref TryItem tryItem)
                {
                    if (tryItem.CaughtTypes == null)
                    {
                        tryItem.CaughtTypes = ExceptionTypesBuilder.Allocate();

                        // Propagate changed value.
                        tryItems[i] = tryItem;
                    }
                }
            }

            protected abstract void HandleDelegateCreation(TextSpan span, IMethodSymbol symbol, ITypeSymbol delegateType);

            protected virtual void HandleThrownExceptionTypes(TextSpan span, ISymbol throwerSymbol, AccessorKinds throwerAccessorKinds)
            {
                var thrownExceptionTypes = GetThrownExceptionTypes(throwerSymbol);

                HandleThrownExceptionTypes(span, throwerSymbol, throwerAccessorKinds, thrownExceptionTypes);
            }

            /// <summary>
            /// Handles exception types thrown by a specified accessors of a symbol, given an array
            /// of documented exception types for that symbol.
            /// </summary>
            protected void HandleThrownExceptionTypes(TextSpan span, ISymbol throwerSymbol, AccessorKinds throwerAccessorKinds, ImmutableArray<DocumentedExceptionType> thrownExceptionTypes)
            {
                var enumerator = throwerAccessorKinds.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var throwerAccessorKind = enumerator.Current;

                    Debug.Assert(_builder.Count == 0);

                    foreach (var thrownExceptionType in thrownExceptionTypes)
                    {
                        if (thrownExceptionType.AccessorKind != throwerAccessorKind)
                            continue;

                        var originalExceptionType = thrownExceptionType.ExceptionType.OriginalDefinition;
                        if (IsTransitive(throwerSymbol, throwerAccessorKind, originalExceptionType))
                            if (!IsIgnoredExceptionType(thrownExceptionType.ExceptionType, originalExceptionType))
                                _builder.Add(thrownExceptionType.ExceptionType);
                    }

                    if (_builder.Count > 0)
                    {
                        var uncaughtTypes = _builder.ToImmutable();
                        _builder.Clear();

                        HandleExceptionTypes(span, throwerSymbol, throwerAccessorKind, uncaughtTypes);
                    }
                }

                bool IsTransitive(ISymbol throwerSymbol, AccessorKind throwerAccessorKind, INamedTypeSymbol originalExceptionType)
                {
                    bool result = IsThrowHelper(throwerSymbol, throwerAccessorKind, originalExceptionType);

                    if (!result)
                    {
                        ImmutableArray<INamedTypeSymbol> intransitiveExceptionTypes;
                        switch (throwerSymbol.DeclaredAccessibility)
                        {
                            case Accessibility.Private:
                                intransitiveExceptionTypes = Context.IntransitiveExceptionTypesPrivate;
                                break;
                            case Accessibility.ProtectedAndInternal:
                            case Accessibility.Internal:
                                intransitiveExceptionTypes = Context.IntransitiveExceptionTypesInternal;
                                break;
                            default:
                                intransitiveExceptionTypes = Context.IntransitiveExceptionTypesPublic;
                                break;
                        }

                        result = !originalExceptionType.HasBaseConversionTo(intransitiveExceptionTypes);
                    }

                    if (Context.DocumentedExceptionTypesProvider.TryGetExceptionAdjustments(throwerSymbol, out var adjustments, CancellationToken))
                        result = ExceptionAdjustments.ApplyFlagAdjustments(result, adjustments, throwerAccessorKind, "transitive", originalExceptionType);

                    return result;
                }

                bool IsThrowHelper(ISymbol throwerSymbol, AccessorKind throwerAccessorKind, INamedTypeSymbol originalExceptionType)
                {
                    if (throwerSymbol.Kind != SymbolKind.Method)
                        return false;

                    bool result = IsThrowHelperName(throwerSymbol.Name);

                    if (Context.DocumentedExceptionTypesProvider.TryGetExceptionAdjustments(throwerSymbol, out var adjustments, CancellationToken))
                        result = ExceptionAdjustments.ApplyFlagAdjustments(result, adjustments, throwerAccessorKind, "thrower", originalExceptionType);

                    return result;

                    static bool IsThrowHelperName(string name)
                    {
                        const string prefix = "Throw";

                        if (name.Length >= 5 && name.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            if (name.Length == 5)
                                return true;
                            char c = name[prefix.Length];
                            return c < 'a' || c > 'z';
                        }
                        else
                        {
                            return false;
                        }
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

                            var bodySyntax = (SyntaxNode?)localFunctionSyntax.Body ?? localFunctionSyntax.ExpressionBody;

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

            protected bool TryDequeueDeferred([NotNullWhen(true)] out ISymbol? symbol, out AccessorKind accessorKind, [NotNullWhen(true)] out SyntaxNode? declarationSyntax, out SyntaxNode? bodySyntax)
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
                    if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
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

            /// <summary>
            /// Handles exception types thrown via a "throw" statement or expression.
            /// </summary>
            private void HandleThrowExceptionTypes(TextSpan span, ExpressionSyntax exceptionExpression)
            {
                Debug.Assert(_builder.Count == 0);

                AccumulateUncaughtTypes(exceptionExpression);

                if (_builder.Count > 0)
                {
                    var uncaughtTypes = _builder.ToImmutable();
                    _builder.Clear();

                    HandleExceptionTypes(span, null, default, uncaughtTypes);
                }

                void AccumulateUncaughtTypes(ExpressionSyntax exceptionExpression)
                {
                    var thrownTypeInfo = SemanticModel.GetTypeInfo(exceptionExpression, CancellationToken);

                    var thrownTypeInfoType = thrownTypeInfo.Type;
                    if (thrownTypeInfoType == null)
                    {
                        if (exceptionExpression.IsKind(SyntaxKind.ConditionalExpression))
                        {
                            var conditionalExpression = (ConditionalExpressionSyntax)exceptionExpression;

                            AccumulateUncaughtTypes(conditionalExpression.WhenTrue);
                            AccumulateUncaughtTypes(conditionalExpression.WhenFalse);

                            return;
                        }
                        else if (exceptionExpression.IsKind(SyntaxKind.NullLiteralExpression))
                        {
                            return;
                        }
                        else if (exceptionExpression.IsKind(SyntaxKind.SwitchExpression))
                        {
                            var switchExpression = (SwitchExpressionSyntax)exceptionExpression;

                            foreach (var arm in switchExpression.Arms)
                                AccumulateUncaughtTypes(arm.Expression);

                            return;
                        }

                        thrownTypeInfoType = thrownTypeInfo.ConvertedType;
                    }

                    if (thrownTypeInfoType is INamedTypeSymbol thrownType && thrownType.TypeKind != TypeKind.Error)
                        if (!IsIgnoredExceptionType(thrownType, thrownType.OriginalDefinition))
                            _builder.Add(thrownType);
                }
            }

            /// <summary>
            /// Handles exception types rethrown via a "throw" statement.
            /// </summary>
            private void HandleRethrowExceptionTypes()
            {
                if (_catchScopes.Count == 0)
                    return;

                // Enclosing "catch" clause determines what exception types may be rethrown.
                var catchScope = _catchScopes.Peek();
                var catchSymbol = catchScope.Symbol;
                var catchFilter = catchScope.Filter;

                bool? blindFilterResult = catchFilter == null
                    ? true
                    : BlindFilterEvaluator.Instance.Visit(catchFilter);

                // If everything passes the filter or if anything passes the filter and the filter
                // does not depend on the exception type, an exception type will be rethrown if it
                // is the "catch" type or a subtype of it.
                if (blindFilterResult == true ||
                    (blindFilterResult != false && catchSymbol == null))
                {
                    for (int i = 0; i < catchScope.TryItems.Count; ++i)
                    {
                        var tryItem = catchScope.TryItems[i];

                        Debug.Assert(_builder.Count == 0);

                        foreach (var exceptionType in tryItem.Types)
                        {
                            // Skip types that have been caught by a prior "catch" clause or will
                            // not be caught by the enclosing "catch" clause.
                            if (tryItem.Caught(exceptionType) || !catchScope.TypeMayCatch(exceptionType))
                                continue;

                            _builder.Add(exceptionType);
                        }

                        if (_builder.Count > 0)
                        {
                            Rethrow(catchScope.TryItems, i, ref tryItem, _builder);

                            _builder.Clear();
                        }
                    }
                }
                // If anything passes the filter and the filter might depend on the exception type,
                // an exception type will be rethrown if it may pass the filter.
                else if (blindFilterResult != false && catchSymbol != null)
                {
                    for (int i = 0; i < catchScope.TryItems.Count; ++i)
                    {
                        var tryItem = catchScope.TryItems[i];

                        Debug.Assert(_builder.Count == 0);

                        foreach (var exceptionType in tryItem.Types)
                        {
                            // Skip types that have been caught by a prior "catch" clause or will
                            // not be caught by the enclosing "catch" clause.
                            if (tryItem.Caught(exceptionType) || !catchScope.TypeMayCatch(exceptionType))
                                continue;

                            _filterEvaluator ??= new FilterEvaluator(SemanticModel);

                            var result = _filterEvaluator.Evaluate(catchFilter!, catchSymbol, exceptionType, rethrow: true, CancellationToken);
                            if (result.Result)
                            {
                                _builder.Add(exceptionType);
                            }
                            else if (result.Subtypes != null)
                            {
                                foreach (var subtype in result.Subtypes)
                                    if (_filterEvaluator.EvaluateSubtype(catchFilter!, catchSymbol, subtype, rethrow: true, CancellationToken))
                                        _builder.Add(subtype);

                                result.Subtypes.Free();
                            }
                        }

                        if (_builder.Count > 0)
                        {
                            Rethrow(catchScope.TryItems, i, ref tryItem, _builder);

                            _builder.Clear();
                        }
                    }
                }

                void Rethrow(List<TryItem> tryItems, int i, ref TryItem tryItem, ExceptionTypesBuilder builder)
                {
                    // If within a "try" block, the exception types that may be rethrown are tracked
                    // in an item in its scope.
                    if (_tryScopes.Count > 0)
                    {
                        var tryScope = _tryScopes.Peek();

                        var uncaughtTypes = builder.ToImmutable();

                        tryScope.Add(new TryItem(tryItem.Span, tryItem.ThrowerSymbol, tryItem.ThrowerAccessorKind, uncaughtTypes));
                    }
                    // Otherwise, the exception types are tracked as rethrown so that they can be
                    // handled as uncaught by the "try" statement.
                    else
                    {
                        if (tryItem.RethrownTypes == null)
                        {
                            tryItem.RethrownTypes = ExceptionTypesBuilder.Allocate();

                            // Propagate changed value.
                            tryItems[i] = tryItem;
                        }

                        foreach (var type in builder)
                            tryItem.RethrownTypes.Add(type);
                    }
                }
            }

            /// <summary>
            /// Handles specified exception types either by tracking them in an enclosing "try"
            /// scope or handling them as uncaught.
            /// </summary>
            private void HandleExceptionTypes(TextSpan span, ISymbol? throwerSymbol, AccessorKind throwerAccessorKind, ImmutableArray<INamedTypeSymbol> exceptionTypes)
            {
                // If within a "try" block, the exception types are tracked in an item in its scope.
                if (_tryScopes.Count > 0)
                {
                    var tryScope = _tryScopes.Peek();

                    tryScope.Add(new TryItem(span, throwerSymbol, throwerAccessorKind, exceptionTypes));
                }
                // Otherwise, the exception types are uncaught.
                else
                {
                    HandleUncaughtExceptionTypes(span, throwerSymbol, throwerAccessorKind, exceptionTypes);
                }
            }

            private bool TryGetCreatedDelegateType(ExpressionSyntax node, [MaybeNullWhen(false)] out ITypeSymbol delegateType)
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
                            case Access.GetAndSet:
                            case Access.Invoke:
                                return;
                            default:
                                Debug.Fail($"Analyzing event symbol but access is {_accessScopes.Peek()}.");
                                return;
                        }

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
                                Debug.Fail($"Analyzing method symbol but access is {_accessScopes.Peek()}.");
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
                                accessorKinds = AccessorKinds.Get | AccessorKinds.Set;
                                break;
                            default:
                                Debug.Fail($"Analyzing property symbol but access is {_accessScopes.Peek()}.");
                                return;
                        }

                        var propertySymbol = (IPropertySymbol)symbol;
                        if (propertySymbol.SetMethod == null)
                        {
                            // "ref"-returning "get" accessor.
                            accessorKinds = AccessorKinds.Get;
                        }
                        break;

                    default:
                        return;
                }

                HandleThrownExceptionTypes(span, symbol, accessorKinds);
            }

            private bool IsIgnoredExceptionType(INamedTypeSymbol exceptionType, INamedTypeSymbol originalExceptionType)
            {
                if (originalExceptionType.HasBaseConversionTo(Context.IgnoredExceptionTypes))
                    return true;

                return IsIgnoredExceptionType(exceptionType);
            }

            protected enum Access
            {
                None,
                Get,
                Set,
                GetAndSet,
                Invoke,
            }
        }

        private sealed class TryScopePool : ObjectPool<List<TryItem>>
        {
            private readonly int _itemMaxCapacity;

            public TryScopePool(int capacity, int itemMaxCapacity)
                : base(capacity)
            {
                _itemMaxCapacity = itemMaxCapacity;
            }

            protected override List<TryItem> Create()
            {
                return new List<TryItem>();
            }

            public override void Return(List<TryItem> item)
            {
                if (item.Capacity > _itemMaxCapacity)
                    return;

                item.Clear();

                _ = ReturnCore(item);
            }
        }

        /// <summary>
        /// Information about exception types that may be thrown from a "try" block if not caught or
        /// if caught and rethrown.
        /// </summary>
        private struct TryItem
        {
            public readonly TextSpan Span;

            public readonly ISymbol? ThrowerSymbol;

            public readonly AccessorKind ThrowerAccessorKind;

            /// <summary>
            /// The exception types that may be thrown.
            /// </summary>
            /// <remarks>
            /// After analyzing the "try" statement, an exception type is considered thrown if it is
            /// in <see cref="Types"/> and is not in <see cref="CaughtTypes"/> or if it is in
            /// <see cref="RethrownTypes"/>.
            /// </remarks>
            public readonly ImmutableArray<INamedTypeSymbol> Types;

            /// <summary>
            /// The exception types that have been caught.
            /// </summary>
            /// <remarks>
            /// After analyzing each "catch" clause, an exception type from <see cref="Types"/> is
            /// added to this collection if the "catch" clause will always catch it.  Based on the
            /// filter one or more subtypes may be added instead.
            /// </remarks>
            public ExceptionTypesBuilder? CaughtTypes;

            /// <summary>
            /// The exception types that may be rethrown.
            /// </summary>
            /// <remarks>
            /// When analyzing a rethrow, an exception type from <see cref="Types"/> is added if it
            /// has not been added to <see cref="CaughtTypes"/> and it may be caught and it is not
            /// captured by another "try" scope.  Based on the filter one or more subtypes may be
            /// added instead.
            /// </remarks>
            public ExceptionTypesBuilder? RethrownTypes;

            public TryItem(TextSpan span, ISymbol? throwerSymbol, AccessorKind throwerAccessorKind, ImmutableArray<INamedTypeSymbol> types)
            {
                Span = span;
                ThrowerSymbol = throwerSymbol;
                ThrowerAccessorKind = throwerAccessorKind;
                Types = types;
                CaughtTypes = null;
                RethrownTypes = null;
            }

            public readonly bool Caught(INamedTypeSymbol exceptionType)
            {
                if (CaughtTypes != null)
                    foreach (var caughtType in CaughtTypes)
                        if (exceptionType.HasBaseConversionTo(caughtType))
                            return true;

                return false;
            }
        }

        /// <summary>
        /// Information about a "catch" clause.
        /// </summary>
        private readonly struct CatchScope
        {
            /// <summary>
            /// Information about exception types that may be thrown from the associated "try"
            /// block.
            /// </summary>
            public readonly List<TryItem> TryItems;

            /// <summary>
            /// The caught exception type, if any.  If <see langword="null"/>, all types are caught.
            /// </summary>
            public readonly INamedTypeSymbol? Type;

            /// <summary>
            /// The symbol of the caught exception, if any.
            /// </summary>
            public readonly ISymbol? Symbol;

            /// <summary>
            /// The filter, if any.
            /// </summary>
            public readonly ExpressionSyntax? Filter;

            public CatchScope(List<TryItem> tryItems, INamedTypeSymbol? type, ISymbol? symbol, ExpressionSyntax? filter)
            {
                TryItems = tryItems;
                Type = type;
                Symbol = symbol;
                Filter = filter;
            }

            public readonly bool TypeWillCatch(INamedTypeSymbol exceptionType)
            {
                return Type == null ||
                       (Type.TypeKind == TypeKind.Class &&
                        exceptionType.HasBaseConversionTo(Type));
            }

            public readonly bool TypeMayCatch(INamedTypeSymbol exceptionType)
            {
                return Type == null ||
                       Type.TypeKind != TypeKind.Class ||
                       exceptionType.HasBaseConversionTo(Type);
            }
        }
    }
}
