// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

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
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var compilation = compilationStartContext.Compilation;
                var additionalFiles = compilationStartContext.Options.AdditionalFiles;
                var cancellationToken = compilationStartContext.CancellationToken;

                var adjustments = ExceptionAdjustments.Load(additionalFiles, cancellationToken);
                var documentedExceptionTypesProvider = GetOrCreateDocumentedExceptionTypesProvider(compilation, adjustments);

                compilationStartContext.RegisterSymbolAction(symbolContext => AnalyzeSymbol(symbolContext, documentedExceptionTypesProvider), SymbolKind.Event, SymbolKind.Method, SymbolKind.Property);
            });
        }

        private static void AnalyzeSymbol(
            SymbolAnalysisContext symbolContext,
            DocumentedExceptionTypesProvider documentedExceptionTypesProvider)
        {
            var symbol = symbolContext.Symbol;

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
            ISymbol overriddenSymbol)
        {
            var cancellationToken = symbolContext.CancellationToken;

            ExceptionTypesBuilder? builder = null;

            var documentedExceptionTypes = documentedExceptionTypesProvider.GetDocumentedExceptionTypes(symbol, cancellationToken);

            foreach (var accessorKind in _accessorKinds)
            {
                // Analyze overridden symbol.
                if (overriddenSymbol != null)
                {
                    if (documentedExceptionTypesProvider.TryGetDocumentedExceptionTypes(overriddenSymbol, out var overriddenDocumentedExceptionTypes, cancellationToken))
                    {
                        foreach (var documentedExceptionType in documentedExceptionTypes)
                        {
                            if (documentedExceptionType.AccessorKind != accessorKind)
                                continue;

                            if (!documentedExceptionType.IsSubsumedBy(overriddenDocumentedExceptionTypes))
                            {
                                if (builder == null)
                                    builder = ExceptionTypesBuilder.Allocate();

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

                        var implementationSymbol = symbol.ContainingType.FindImplementationForInterfaceMember(interfaceMemberSymbol);
                        if (implementationSymbol != null && SymbolEqualityComparer.Default.Equals(implementationSymbol, symbol))
                        {
                            if (documentedExceptionTypesProvider.TryGetDocumentedExceptionTypes(interfaceMemberSymbol, out var implementedDocumentedExceptionTypes, cancellationToken))
                            {
                                foreach (var documentedExceptionType in documentedExceptionTypes)
                                {
                                    if (documentedExceptionType.AccessorKind != accessorKind)
                                        continue;

                                    if (!documentedExceptionType.IsSubsumedBy(implementedDocumentedExceptionTypes))
                                    {
                                        if (builder == null)
                                            builder = ExceptionTypesBuilder.Allocate();

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
        }

        private static void ReportDiagnostic(SymbolAnalysisContext symbolContext, Location location, AccessorKind accessorKind, INamedTypeSymbol supertype, ISymbol supertypeMember, ImmutableArray<INamedTypeSymbol> exceptionTypes)
        {
            string exceptionTypeIds = string.Join(",", exceptionTypes.Select(x => x.OriginalDefinition.GetDocumentationCommentId()));

            string? accessor = DocumentedExceptionTypesProvider.GetAccessorName(accessorKind);

            var builder = ImmutableDictionary.CreateBuilder<string, string>();

            builder.Add(PropertyKeys.ExceptionTypeIds, exceptionTypeIds);

            if (supertypeMember != null)
            {
                string supertypeMemberId = supertypeMember.OriginalDefinition.GetDocumentationCommentId();
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
