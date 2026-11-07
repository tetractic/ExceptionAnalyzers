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
    /// <summary>
    /// Statically evaluates a catch filter expression.
    /// </summary>
    internal sealed class BlindFilterEvaluator : CSharpSyntaxVisitor<bool?>
    {
        public static readonly BlindFilterEvaluator Instance = new BlindFilterEvaluator();

        private BlindFilterEvaluator()
        {
        }

        public override bool? DefaultVisit(SyntaxNode node)
        {
            return null;
        }

        public override bool? VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.LogicalOrExpression))
            {
                bool? leftResult = Visit(node.Left);
                if (leftResult == true)
                    return true;

                bool? rightResult = Visit(node.Right);
                if (rightResult == true)
                    return true;

                if (leftResult == false && rightResult == false)
                    return false;

                return null;
            }
            else if (node.IsKind(SyntaxKind.LogicalAndExpression))
            {
                bool? leftResult = Visit(node.Left);
                if (leftResult == false)
                    return false;

                bool? rightResult = Visit(node.Right);
                if (rightResult == false)
                    return false;

                if (leftResult == true && rightResult == true)
                    return true;

                return null;
            }
            else
            {
                return DefaultVisit(node);
            }
        }

        public override bool? VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            bool? conditionResult = Visit(node.Condition);
            if (conditionResult == true)
                return Visit(node.WhenTrue);
            if (conditionResult == false)
                return Visit(node.WhenFalse);

            bool? trueResult = Visit(node.WhenTrue);
            bool? falseResult = Visit(node.WhenFalse);
            if (trueResult == falseResult)
                return trueResult;

            return null;
        }

        public override bool? VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.TrueLiteralExpression))
            {
                return true;
            }
            else if (node.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                return false;
            }
            else
            {
                return DefaultVisit(node);
            }
        }

        public override bool? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            return Visit(node.Expression);
        }

        public override bool? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.LogicalNotExpression))
            {
                return !Visit(node.Operand);
            }
            else
            {
                return DefaultVisit(node);
            }
        }
    }
}
