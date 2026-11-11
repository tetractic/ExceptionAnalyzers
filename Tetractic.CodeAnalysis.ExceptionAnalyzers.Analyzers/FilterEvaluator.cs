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
using System.Threading;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    /// <summary>
    /// Statically evaluates a catch filter expression under the assumption that the exception is of
    /// a particular type.
    /// </summary>
    internal sealed class FilterEvaluator : CSharpSyntaxVisitor<bool?>
    {
        private readonly SemanticModel _semanticModel;

        private ISymbol _symbol = null!;

        private INamedTypeSymbol _type = null!;

        private CancellationToken _cancellationToken;

        private bool _collectSubtypes;

        private ExceptionTypesBuilder? _subtypes;

        private bool _allowSubtypes;

        public FilterEvaluator(SemanticModel semanticModel)
        {
            _semanticModel = semanticModel;
        }

        public (bool Result, ExceptionTypesBuilder? Subtypes) Evaluate(
            SyntaxNode filter,
            ISymbol symbol,
            INamedTypeSymbol type,
            bool rethrow,
            CancellationToken cancellationToken)
        {
            _symbol = symbol;
            _type = type;
            _cancellationToken = cancellationToken;
            _collectSubtypes = true;
            _subtypes = null;
            _allowSubtypes = true;

            bool? rawResult = Visit(filter);

            // If rethrowing and the result was "maybe", the result would become "true" and the
            // subtypes would be discarded.  Assuming the type is not one of the subtypes, if the
            // result is now "false", the type must be one of the subtypes in order to be rethrown.
            if (rethrow && rawResult == null && _subtypes != null && _subtypes.Count > 0)
            {
                _collectSubtypes = false;
                _allowSubtypes = false;

                rawResult = Visit(filter);
            }

            bool result = rawResult ?? rethrow;

            if (result)
            {
                _subtypes?.Free();
                _subtypes = null;
            }

            return (result, _subtypes);
        }

        public bool EvaluateSubtype(
            SyntaxNode filter,
            ISymbol symbol,
            INamedTypeSymbol type,
            bool rethrow,
            CancellationToken cancellationToken)
        {
            _symbol = symbol;
            _type = type;
            _cancellationToken = cancellationToken;
            _collectSubtypes = false;
            _subtypes = null;
            _allowSubtypes = true;

            return Visit(filter) ?? rethrow;
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
            else if (node.IsKind(SyntaxKind.IsExpression))
            {
                if (_type.TypeKind != TypeKind.Class)
                    return null;

                var symbol = _semanticModel.GetSymbolInfo(node.Left, _cancellationToken).Symbol;

                if (SymbolEqualityComparer.Default.Equals(symbol, _symbol))
                {
                    var typeSymbol = _semanticModel.GetSymbolInfo(node.Right, _cancellationToken).Symbol;

                    if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
                    {
                        if (namedTypeSymbol.TypeKind != TypeKind.Class)
                            return null;

                        if (_type.HasBaseConversionTo(namedTypeSymbol))
                            return true;

                        if (namedTypeSymbol.HasBaseConversionTo(_type))
                        {
                            if (_collectSubtypes)
                            {
                                _subtypes ??= ExceptionTypesBuilder.Allocate();
                                _subtypes.Add(namedTypeSymbol);
                            }

                            if (_allowSubtypes)
                                return null;
                        }

                        return false;
                    }
                }

                return DefaultVisit(node);
            }
            else
            {
                return DefaultVisit(node);
            }
        }

        public override bool? VisitBinaryPattern(BinaryPatternSyntax node)
        {
            if (node.IsKind(SyntaxKind.OrPattern))
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
            else if (node.IsKind(SyntaxKind.AndPattern))
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

        public override bool? VisitConstantPattern(ConstantPatternSyntax node)
        {
            var typeSymbol = _semanticModel.GetSymbolInfo(node.Expression, _cancellationToken).Symbol;

            if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
            {
                if (namedTypeSymbol.TypeKind != TypeKind.Class)
                    return null;

                if (_type.HasBaseConversionTo(namedTypeSymbol))
                    return true;

                if (namedTypeSymbol.HasBaseConversionTo(_type))
                {
                    if (_collectSubtypes)
                    {
                        _subtypes ??= ExceptionTypesBuilder.Allocate();
                        _subtypes.Add(namedTypeSymbol);
                    }

                    if (_allowSubtypes)
                        return null;
                }

                return false;
            }

            return DefaultVisit(node);
        }

        public override bool? VisitDeclarationPattern(DeclarationPatternSyntax node)
        {
            var typeSymbol = _semanticModel.GetSymbolInfo(node.Type, _cancellationToken).Symbol;

            if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
            {
                if (namedTypeSymbol.TypeKind != TypeKind.Class)
                    return null;

                if (_type.HasBaseConversionTo(namedTypeSymbol))
                    return true;

                if (namedTypeSymbol.HasBaseConversionTo(_type))
                {
                    if (_collectSubtypes)
                    {
                        _subtypes ??= ExceptionTypesBuilder.Allocate();
                        _subtypes.Add(namedTypeSymbol);
                    }

                    if (_allowSubtypes)
                        return null;
                }

                return false;
            }

            return DefaultVisit(node);
        }

        public override bool? VisitIsPatternExpression(IsPatternExpressionSyntax node)
        {
            if (_type.TypeKind != TypeKind.Class)
                return null;

            var symbol = _semanticModel.GetSymbolInfo(node.Expression, _cancellationToken).Symbol;

            if (SymbolEqualityComparer.Default.Equals(symbol, _symbol))
                return Visit(node.Pattern);

            return DefaultVisit(node);
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

        public override bool? VisitParenthesizedPattern(ParenthesizedPatternSyntax node)
        {
            return Visit(node.Pattern);
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

        public override bool? VisitUnaryPattern(UnaryPatternSyntax node)
        {
            if (node.IsKind(SyntaxKind.NotPattern))
            {
                return !Visit(node.Pattern);
            }
            else
            {
                return DefaultVisit(node);
            }
        }
    }
}
