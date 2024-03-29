// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed partial class SupertypeExceptionsAnalyzer : DocumentedExceptionsAnalyzerBase
    {
        public const string DiagnosticId = "Ex0200";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: "Member is documented as throwing exception not documented on member in base or interface type",
            messageFormat: "'{0}' is documented as throwing exception not documented on member in '{1}': {2}",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public const string AccessorDiagnosticId = "Ex0201";

        internal static readonly DiagnosticDescriptor AccessorRule = new DiagnosticDescriptor(
            id: AccessorDiagnosticId,
            title: "Member accessor is documented as throwing exception not documented on member in base or interface type",
            messageFormat: "'{0}' '{1}' accessor is documented as throwing exception not documented on member in '{2}': {3}",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly AccessorKind[] _accessorKinds = (AccessorKind[])typeof(AccessorKind).GetEnumValues();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Rule,
            AccessorRule,
        });

        public override void Initialize(AnalysisContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var compilation = compilationStartContext.Compilation;
                var additionalFiles = compilationStartContext.Options.AdditionalFiles;
                var cancellationToken = compilationStartContext.CancellationToken;

                var exceptionAdjustments = ExceptionAdjustments.Load(additionalFiles, cancellationToken);
                var documentedExceptionTypesProvider = GetOrCreateDocumentedExceptionTypesProvider(compilation, exceptionAdjustments);

                compilationStartContext.RegisterSymbolAction(symbolContext => AnalyzeSymbol(symbolContext, documentedExceptionTypesProvider), SymbolKind.Event, SymbolKind.Method, SymbolKind.Property);
            });
        }

        private static void AnalyzeSymbol(
            SymbolAnalysisContext symbolContext,
            DocumentedExceptionTypesProvider documentedExceptionTypesProvider)
        {
            var symbol = symbolContext.Symbol;

            var syntaxReferences = symbol.DeclaringSyntaxReferences;
            if (syntaxReferences.Length == 0)
                return;
            var syntaxReference = syntaxReferences[0];
            var syntaxTree = syntaxReference.SyntaxTree;

            // Analyzer requires documentation comments.
            if (syntaxTree.Options.DocumentationMode == DocumentationMode.None)
                return;

            switch (symbol.Kind)
            {
                case SymbolKind.Event:
                {
                    var eventSymbol = (IEventSymbol)symbol;

                    ReportUnexpectedExceptionTypes(symbolContext, documentedExceptionTypesProvider, symbol, eventSymbol.OverriddenEvent);
                    break;
                }

                case SymbolKind.Method:
                {
                    var methodSymbol = (IMethodSymbol)symbol;

                    ReportUnexpectedExceptionTypes(symbolContext, documentedExceptionTypesProvider, symbol, methodSymbol.OverriddenMethod);
                    break;
                }

                case SymbolKind.Property:
                {
                    var propertySymbol = (IPropertySymbol)symbol;

                    ReportUnexpectedExceptionTypes(symbolContext, documentedExceptionTypesProvider, symbol, propertySymbol.OverriddenProperty);
                    break;
                }
            }
        }

        private static void ReportUnexpectedExceptionTypes(
            SymbolAnalysisContext symbolContext,
            DocumentedExceptionTypesProvider documentedExceptionTypesProvider,
            ISymbol symbol,
            ISymbol? overriddenSymbol)
        {
            var cancellationToken = symbolContext.CancellationToken;

            ExceptionTypesBuilder? builder = null;

            var documentedExceptionTypes = documentedExceptionTypesProvider.GetDocumentedExceptionTypes(symbol, cancellationToken);

            foreach (var accessorKind in _accessorKinds)
            {
                // Analyze overridden symbol.
                if (overriddenSymbol != null)
                {
                    if (TryGetSupertypeMemberDocumentedExceptionTypes(overriddenSymbol, out var overriddenDocumentedExceptionTypes, cancellationToken))
                    {
                        foreach (var documentedExceptionType in documentedExceptionTypes)
                        {
                            if (documentedExceptionType.AccessorKind != accessorKind)
                                continue;

                            if (!documentedExceptionType.IsSubsumedBy(overriddenDocumentedExceptionTypes))
                            {
                                builder ??= ExceptionTypesBuilder.Allocate();

                                builder.Add(documentedExceptionType.ExceptionType);
                            }
                        }

                        if (builder != null && builder.Count > 0)
                        {
                            var unexpectedExceptionTypes = builder.ToImmutable();
                            builder.Clear();

                            var location = symbol.GetFirstLocationOrNone();

                            ReportDiagnostic(symbolContext, location, accessorKind, overriddenSymbol.ContainingType, overriddenSymbol, unexpectedExceptionTypes);
                        }
                    }
                }

                // Analyze implemented symbol(s).
                foreach (var interfaceSymbol in symbol.ContainingType.AllInterfaces)
                {
                    foreach (var interfaceMemberSymbol in interfaceSymbol.GetMembers())
                    {
                        if (interfaceMemberSymbol.Kind != symbol.Kind)
                            continue;

                        switch (interfaceMemberSymbol.Kind)
                        {
                            case SymbolKind.Event:
                                var interfaceEventSymbol = (IEventSymbol)interfaceMemberSymbol;
                                if (accessorKind == AccessorKind.Add && interfaceEventSymbol.AddMethod == null)
                                    continue;
                                if (accessorKind == AccessorKind.Remove && interfaceEventSymbol.RemoveMethod == null)
                                    continue;
                                break;

                            case SymbolKind.Property:
                                var interfacePropertySymbol = (IPropertySymbol)interfaceMemberSymbol;
                                if (accessorKind == AccessorKind.Get && interfacePropertySymbol.GetMethod == null)
                                    continue;
                                if (accessorKind == AccessorKind.Set && interfacePropertySymbol.SetMethod == null)
                                    continue;
                                break;
                        }

                        var implementationSymbol = symbol.ContainingType.FindImplementationForInterfaceMember(interfaceMemberSymbol);
                        if (implementationSymbol != null && SymbolEqualityComparer.Default.Equals(implementationSymbol, symbol))
                        {
                            if (TryGetSupertypeMemberDocumentedExceptionTypes(interfaceMemberSymbol, out var implementedDocumentedExceptionTypes, cancellationToken))
                            {
                                foreach (var documentedExceptionType in documentedExceptionTypes)
                                {
                                    if (documentedExceptionType.AccessorKind != accessorKind)
                                        continue;

                                    if (!documentedExceptionType.IsSubsumedBy(implementedDocumentedExceptionTypes))
                                    {
                                        builder ??= ExceptionTypesBuilder.Allocate();

                                        builder.Add(documentedExceptionType.ExceptionType);
                                    }
                                }

                                if (builder != null && builder.Count > 0)
                                {
                                    var unexpectedExceptionTypes = builder.ToImmutable();
                                    builder.Clear();

                                    var location = symbol.GetFirstLocationOrNone();

                                    ReportDiagnostic(symbolContext, location, accessorKind, interfaceSymbol, interfaceMemberSymbol, unexpectedExceptionTypes);
                                }
                            }
                        }
                    }
                }
            }

            builder?.Free();

            bool TryGetSupertypeMemberDocumentedExceptionTypes(ISymbol symbol, out ImmutableArray<DocumentedExceptionType> documentedExceptionTypes, CancellationToken cancellationToken)
            {
                if (documentedExceptionTypesProvider.TryGetDocumentedExceptionTypes(symbol, out documentedExceptionTypes, cancellationToken))
                    return true;

                // If the supertype member is in the compilation then assume that undocumented means
                // no exception types are thrown.  The user can document exception types on the
                // supertype member if necessary.
                if (SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, symbolContext.Compilation.Assembly))
                {
                    documentedExceptionTypes = ImmutableArray<DocumentedExceptionType>.Empty;
                    return true;
                }

                documentedExceptionTypes = default;
                return false;
            }
        }

        private static void ReportDiagnostic(SymbolAnalysisContext symbolContext, Location location, AccessorKind accessorKind, INamedTypeSymbol supertype, ISymbol supertypeMember, ImmutableArray<INamedTypeSymbol> exceptionTypes)
        {
            string exceptionTypeIds = string.Join(",", exceptionTypes.Select(x => x.GetDeclarationDocumentationCommentId()));

            string? accessor = DocumentedExceptionType.GetAccessorName(accessorKind);

            var builder = ImmutableDictionary.CreateBuilder<string, string?>();

            builder.Add(PropertyKeys.ExceptionTypeIds, exceptionTypeIds);

            if (supertypeMember != null)
            {
                string? supertypeMemberId = supertypeMember.GetDeclarationDocumentationCommentId();
                if (supertypeMemberId != null)
                {
                    builder.Add(PropertyKeys.SupertypeMemberId, supertypeMemberId);
                    if (accessor != null)
                        builder.Add(PropertyKeys.SupertypeAccessor, accessor);
                }
            }

            var properties = builder.ToImmutable();

            string symbolName = symbolContext.Symbol.ToDisplayString(MemberDiagnosticDisplayFormat);

            string supertypeName = supertype.ToDisplayString(TypeDiagnosticDisplayFormat);

            string exceptionNames = string.Join(", ", exceptionTypes.Select(x => x.ToDisplayString(TypeDiagnosticDisplayFormat)));

            if (accessorKind == AccessorKind.Unspecified)
            {
                symbolContext.ReportDiagnostic(Diagnostic.Create(
                    descriptor: Rule,
                    location: location,
                    properties: properties,
                    messageArgs: new[] { symbolName, supertypeName, exceptionNames }));
            }
            else
            {
                symbolContext.ReportDiagnostic(Diagnostic.Create(
                    descriptor: AccessorRule,
                    location: location,
                    properties: properties,
                    messageArgs: new[] { symbolName, accessor, supertypeName, exceptionNames }));
            }
        }

        internal static class PropertyKeys
        {
            public const string ExceptionTypeIds = nameof(ExceptionTypeIds);

            public const string SupertypeMemberId = nameof(SupertypeMemberId);

            public const string SupertypeAccessor = nameof(SupertypeAccessor);
        }
    }
}
