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
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    public partial class MemberExceptionsAnalyzer
    {
        private sealed class MemberVisitor : Visitor
        {
            // Member to report in diagnostic.
            private ISymbol _symbol = null!;

            // Exception types documented on member.
            private ImmutableArray<DocumentedExceptionType> _documentedExceptionTypes;

            // Accessor to report is diagnostic and to use in filtering documented exception types.
            private AccessorKind _accessorKind;

            private ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> _localAdjustments = null!;

            public MemberVisitor(SemanticModelAnalysisContext semanticModelContext, Context context)
                : base(semanticModelContext, context)
            {
            }

            public void Analyze(FieldDeclarationSyntax fieldSyntax)
            {
                foreach (var variableSyntax in fieldSyntax.Declaration.Variables)
                {
                    if (variableSyntax.Initializer != null)
                    {
                        var symbol = SemanticModel.GetDeclaredSymbol(variableSyntax, CancellationToken);
                        if (symbol == null)
                            throw new UnreachableException();

                        Analyze(symbol, AccessorKind.Unspecified, fieldSyntax, variableSyntax.Initializer);
                    }
                }
            }

            public void Analyze(BaseMethodDeclarationSyntax baseMethodSyntax)
            {
                var symbol = SemanticModel.GetDeclaredSymbol(baseMethodSyntax, CancellationToken);
                if (symbol == null)
                    throw new UnreachableException();

                var bodySyntax = (SyntaxNode?)baseMethodSyntax.Body ?? baseMethodSyntax.ExpressionBody;

                Analyze(symbol, AccessorKind.Unspecified, baseMethodSyntax, bodySyntax);
            }

            public void Analyze(BasePropertyDeclarationSyntax basePropertySyntax)
            {
                var symbol = SemanticModel.GetDeclaredSymbol(basePropertySyntax, CancellationToken);
                if (symbol == null)
                    throw new UnreachableException();

                // Analyze expression body.
                switch (basePropertySyntax.Kind())
                {
                    case SyntaxKind.PropertyDeclaration:
                    {
                        var propertySyntax = (PropertyDeclarationSyntax)basePropertySyntax;
                        if (propertySyntax.ExpressionBody != null)
                            Analyze(symbol, AccessorKind.Get, propertySyntax, propertySyntax.ExpressionBody);
                        if (propertySyntax.Initializer != null)
                            Analyze(symbol, AccessorKind.Unspecified, propertySyntax, propertySyntax.Initializer);
                        break;
                    }
                    case SyntaxKind.IndexerDeclaration:
                    {
                        var indexerSyntax = (IndexerDeclarationSyntax)basePropertySyntax;
                        if (indexerSyntax.ExpressionBody != null)
                            Analyze(symbol, AccessorKind.Get, indexerSyntax, indexerSyntax.ExpressionBody);
                        break;
                    }
                }

                // Analyze accessors.
                if (basePropertySyntax.AccessorList != null)
                {
                    foreach (var accessorSyntax in basePropertySyntax.AccessorList.Accessors)
                    {
                        var accessorKind = accessorSyntax.Kind() switch
                        {
                            SyntaxKind.GetAccessorDeclaration => AccessorKind.Get,
                            SyntaxKind.SetAccessorDeclaration => AccessorKind.Set,
                            SyntaxKind.AddAccessorDeclaration => AccessorKind.Add,
                            SyntaxKind.RemoveAccessorDeclaration => AccessorKind.Remove,
                            _ => AccessorKind.Unspecified,
                        };

                        var bodySyntax = (SyntaxNode?)accessorSyntax.Body ?? accessorSyntax.ExpressionBody;

                        Analyze(symbol, accessorKind, basePropertySyntax, bodySyntax);
                    }
                }
            }

            public void Analyze(ConstructorDeclarationSyntax constructorSyntax)
            {
                var symbol = SemanticModel.GetDeclaredSymbol(constructorSyntax, CancellationToken);
                if (symbol == null)
                    throw new UnreachableException();

                if (constructorSyntax.Initializer != null)
                {
                    Analyze(symbol, AccessorKind.Unspecified, constructorSyntax, constructorSyntax.Initializer);
                }
                else
                {
                    var baseType = symbol.ContainingType.BaseType;
                    if (baseType != null)
                    {
                        var baseConstructor = baseType.GetParameterlessConstructor();
                        if (baseConstructor != null)
                        {
                            _symbol = symbol;
                            _accessorKind = AccessorKind.Unspecified;
                            _documentedExceptionTypes = GetDocumentedExceptionTypes(symbol);

                            _localAdjustments = GetAdjustmentsFromComments(constructorSyntax);

                            var span = constructorSyntax.Identifier.Span;
                            HandleThrownExceptionTypes(span, baseConstructor, AccessorKinds.Unspecified);
                        }
                    }
                }

                var bodySyntax = (SyntaxNode?)constructorSyntax.Body ?? constructorSyntax.ExpressionBody;

                Analyze(symbol, AccessorKind.Unspecified, constructorSyntax, bodySyntax);
            }

            protected override void HandleDelegateCreation(TextSpan span, IMethodSymbol symbol, ITypeSymbol delegateType)
            {
                var thrownExceptionTypes = GetThrownExceptionTypes(symbol);
                if (thrownExceptionTypes.IsEmpty)
                    return;

                var documentedExceptionTypes = GetDocumentedExceptionTypes(delegateType);

                ExceptionTypesBuilder? builder = null;

                foreach (var thrownExceptionType in thrownExceptionTypes)
                {
                    if (thrownExceptionType.AccessorKind != AccessorKind.Unspecified)
                        continue;

                    var originalExceptionType = thrownExceptionType.ExceptionType.OriginalDefinition;
                    var originalThrownExceptionType = new DocumentedExceptionType(originalExceptionType, thrownExceptionType.AccessorKind);
                    if (!originalExceptionType.HasBaseConversionTo(Context.IgnoredExceptionTypes) &&
                        !originalThrownExceptionType.IsSubsumedBy(documentedExceptionTypes))
                    {
                        if (builder == null)
                            builder = ExceptionTypesBuilder.Allocate();

                        builder.Add(thrownExceptionType.ExceptionType);
                    }
                }

                if (builder != null)
                {
                    var unexpectedExceptionTypes = builder.ToImmutable();
                    builder.Free();

                    string exceptionDisplayNames = string.Join(", ", unexpectedExceptionTypes.Select(x => x.ToDisplayString(TypeDiagnosticDisplayFormat)));

                    if (symbol.MethodKind == MethodKind.AnonymousFunction)
                    {
                        SemanticModelContext.ReportDiagnostic(Diagnostic.Create(
                            descriptor: AnonymousDelegateCreationRule,
                            location: SemanticModel.SyntaxTree.GetLocation(span),
                            properties: ImmutableDictionary<string, string?>.Empty,
                            messageArgs: new[] { delegateType.Name, exceptionDisplayNames }));
                    }
                    else
                    {
                        SemanticModelContext.ReportDiagnostic(Diagnostic.Create(
                            descriptor: DelegateCreationRule,
                            location: SemanticModel.SyntaxTree.GetLocation(span),
                            properties: ImmutableDictionary<string, string?>.Empty,
                            messageArgs: new[] { delegateType.Name, symbol.Name, exceptionDisplayNames }));
                    }
                }
            }

            protected override void HandleThrownExceptionTypes(TextSpan span, ISymbol throwerSymbol, AccessorKinds throwerAccessorKinds)
            {
                var thrownExceptionTypes = GetThrownExceptionTypes(throwerSymbol);

                thrownExceptionTypes = ExceptionAdjustments.ApplyAdjustments(thrownExceptionTypes, _localAdjustments, throwerSymbol, Compilation);

                HandleThrownExceptionTypes(span, throwerSymbol, throwerAccessorKinds, thrownExceptionTypes);
            }

            protected override bool IsIgnoredExceptionType(INamedTypeSymbol exceptionType)
            {
                // Ignore all exception types on undocumented local functions.
                if (_documentedExceptionTypes.IsDefault)
                    return true;

                var originalExceptionType = exceptionType.OriginalDefinition;
                var originalThrownExceptionType = new DocumentedExceptionType(originalExceptionType, _accessorKind);
                return originalThrownExceptionType.IsSubsumedBy(_documentedExceptionTypes);
            }

            protected override void HandleUncaughtExceptionTypes(TextSpan span, ISymbol? throwerSymbol, AccessorKind throwerAccessorKind, ImmutableArray<INamedTypeSymbol> exceptionTypes)
            {
                string exceptionTypeIds = string.Join(",", exceptionTypes.Select(x => x.GetDeclarationDocumentationCommentId()));

                string? accessor = DocumentedExceptionType.GetAccessorName(_accessorKind);

                var builder = ImmutableDictionary.CreateBuilder<string, string?>();

                builder.Add(PropertyKeys.ExceptionTypeIds, exceptionTypeIds);

                builder.Add(PropertyKeys.MemberId, _symbol.GetDeclarationDocumentationCommentId());
                if (accessor != null)
                    builder.Add(PropertyKeys.Accessor, accessor);

                if (throwerSymbol != null)
                {
                    string? throwerMemberId = throwerSymbol.GetDeclarationDocumentationCommentId();
                    if (throwerMemberId != null)
                    {
                        string? throwerAccessor = DocumentedExceptionType.GetAccessorName(throwerAccessorKind);

                        builder.Add(PropertyKeys.ThrowerMemberId, throwerMemberId);
                        if (throwerAccessor != null)
                            builder.Add(PropertyKeys.ThrowerAccessor, throwerAccessor);
                    }
                }

                var properties = builder.ToImmutable();

                string symbolName;

                string exceptionNames = string.Join(", ", exceptionTypes.Select(x => x.ToDisplayString(TypeDiagnosticDisplayFormat)));

                if (_accessorKind == AccessorKind.Unspecified)
                {
                    if (_symbol.Kind == SymbolKind.Field ||
                        _symbol.Kind == SymbolKind.Property)
                    {
                        symbolName = _symbol.ToDisplayString(MemberDiagnosticDisplayFormat);

                        SemanticModelContext.ReportDiagnostic(Diagnostic.Create(
                            descriptor: InitializerRule,
                            location: SemanticModel.SyntaxTree.GetLocation(span),
                            properties: properties,
                            messageArgs: new[] { symbolName, exceptionNames }));
                        return;
                    }
                    else if (_symbol.Kind == SymbolKind.Method)
                    {
                        var methodSymbol = (IMethodSymbol)_symbol;
                        switch (methodSymbol.MethodKind)
                        {
                            case MethodKind.Constructor:
                            case MethodKind.StaticConstructor:
                            {
                                symbolName = methodSymbol.ToDisplayString(ConstructorDiagnosticDisplayFormat);

                                SemanticModelContext.ReportDiagnostic(Diagnostic.Create(
                                    descriptor: Rule,
                                    location: SemanticModel.SyntaxTree.GetLocation(span),
                                    properties: properties,
                                    messageArgs: new[] { symbolName, exceptionNames }));
                                return;
                            }
                        }
                    }

                    symbolName = _symbol.ToDisplayString(MemberDiagnosticDisplayFormat);

                    SemanticModelContext.ReportDiagnostic(Diagnostic.Create(
                        descriptor: Rule,
                        location: SemanticModel.SyntaxTree.GetLocation(span),
                        properties: properties,
                        messageArgs: new[] { symbolName, exceptionNames }));
                }
                else
                {
                    symbolName = _symbol.ToDisplayString(MemberDiagnosticDisplayFormat);

                    SemanticModelContext.ReportDiagnostic(Diagnostic.Create(
                        descriptor: AccessorRule,
                        location: SemanticModel.SyntaxTree.GetLocation(span),
                        properties: properties,
                        messageArgs: new[] { symbolName, accessor, exceptionNames }));
                }
            }

            private void Analyze(ISymbol symbol, AccessorKind accessorKind, SyntaxNode declarationSyntax, SyntaxNode? bodySyntax)
            {
                _symbol = symbol;
                _documentedExceptionTypes = GetDocumentedExceptionTypes(symbol);
                _accessorKind = accessorKind;

                for (; ; )
                {
                    _localAdjustments = GetAdjustmentsFromComments(declarationSyntax);

                    Visit(bodySyntax, Access.Get);

                    if (!TryDequeueDeferred(out _symbol!, out _accessorKind, out declarationSyntax!, out bodySyntax!))
                        break;

                    _ = TryGetDocumentedExceptionTypes(_symbol, out _documentedExceptionTypes);
                }
            }
        }
    }
}
