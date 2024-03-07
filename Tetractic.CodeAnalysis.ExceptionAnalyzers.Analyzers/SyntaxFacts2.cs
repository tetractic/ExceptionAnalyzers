// Copyright 2021 Carl Reinke
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

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal static class SyntaxFacts2
    {
        internal static bool HasYieldStatement(SyntaxNode? node)
        {
            if (node != null)
            {
                foreach (var child in node.DescendantNodesAndSelf(IsNotFunctionOrExpression))
                {
                    if (child.IsKind(SyntaxKind.YieldReturnStatement) ||
                        child.IsKind(SyntaxKind.YieldBreakStatement))
                    {
                            return true;
                    }
                }
            }

            return false;
        }

        private static bool IsNotFunctionOrExpression(SyntaxNode node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.AnonymousMethodExpression:
                case SyntaxKind.SimpleLambdaExpression:
                case SyntaxKind.ParenthesizedLambdaExpression:
                case SyntaxKind.LocalFunctionStatement:
                    return false;
                default:
                    return !(node is ExpressionSyntax);
            }
        }
    }
}
