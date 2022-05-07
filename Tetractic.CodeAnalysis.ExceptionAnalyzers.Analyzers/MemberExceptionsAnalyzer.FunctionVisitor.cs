// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    public partial class MemberExceptionsAnalyzer
    {
        private sealed class FunctionVisitor : Visitor
        {
            private readonly SymbolStack _symbolStack = new SymbolStack();

            private readonly Stack<ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>>> _localAdjustmentsStack = new Stack<ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>>>();

            public FunctionVisitor(SemanticModelAnalysisContext semanticModelContext, Context context)
                : base(semanticModelContext, context)
            {
            }

            public DocumentedExceptionTypesBuilder ThrownExceptionTypesBuilder { get; } = new DocumentedExceptionTypesBuilder();

            public void Analyze(IMethodSymbol symbol, SyntaxNode declarationSyntax, SyntaxNode? bodySyntax)
            {
                _symbolStack.Push(symbol);
                try
                {
                    _localAdjustmentsStack.Push(GetAdjustmentsFromComments(declarationSyntax));
                    try
                    {
                        Visit(bodySyntax, Access.Get);
                    }
                    finally
                    {
                        _ = _localAdjustmentsStack.Pop();
                    }
                }
                finally
                {
                    _ = _symbolStack.Pop();
                }

                Debug.Assert(!TryDequeDeferred(out _, out _, out _, out _), "Local function visitor deferred analysis.");
            }

            public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
            {
                // Do not analyze local function as part of the local function body.
            }

            public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
            {
                // Don't analyze lambda as part of method body.
            }

            public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
            {
                // Don't analyze lambda as part of method body.
            }

            protected override void HandleDelegateCreation(TextSpan span, IMethodSymbol symbol, ITypeSymbol delegateType)
            {
            }

            protected override void HandleThrownExceptionTypes(TextSpan span, ISymbol throwerSymbol, AccessorKinds throwerAccessorKinds)
            {
                if (TryGetDocumentedExceptionTypes(throwerSymbol, out var documentedExceptionTypes))
                {
                    var localAdjustments = _localAdjustmentsStack.Peek();

                    documentedExceptionTypes = ExceptionAdjustments.ApplyAdjustments(documentedExceptionTypes, localAdjustments, throwerSymbol, Compilation);

                    HandleThrownExceptionTypes(span, throwerSymbol, throwerAccessorKinds, documentedExceptionTypes);
                    return;
                }

                // Recursively analyze local function calls.
                if (throwerSymbol.Kind == SymbolKind.Method)
                {
                    var methodSymbol = (IMethodSymbol)throwerSymbol;
                    if (methodSymbol.MethodKind == MethodKind.LocalFunction)
                    {
                        // Prevent infinite recursion.
                        if (_symbolStack.Contains(methodSymbol))
                            return;

                        foreach (var syntaxReference in methodSymbol.DeclaringSyntaxReferences)
                        {
                            var localFunctionSyntax = (LocalFunctionStatementSyntax)syntaxReference.GetSyntax(CancellationToken);

                            var bodySyntax = (SyntaxNode?)localFunctionSyntax.Body ?? localFunctionSyntax.ExpressionBody;

                            Analyze(methodSymbol, localFunctionSyntax, bodySyntax);
                            return;
                        }
                    }
                }
            }

            protected override bool IsIgnoredExceptionType(INamedTypeSymbol exceptionType) => false;

            protected override void HandleUncaughtExceptionType(TextSpan span, ISymbol? symbol, AccessorKind accessorKind, INamedTypeSymbol exceptionType)
            {
                ThrownExceptionTypesBuilder.Add(exceptionType, AccessorKind.Unspecified);
            }

            protected override void HandleUncaughtExceptionTypes(TextSpan span, ISymbol? symbol, AccessorKind accessorKind, ImmutableArray<INamedTypeSymbol> exceptionTypes)
            {
                foreach (var exceptionType in exceptionTypes)
                    ThrownExceptionTypesBuilder.Add(exceptionType, AccessorKind.Unspecified);
            }
        }
    }
}
