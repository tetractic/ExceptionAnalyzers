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
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ImplicitConstructorExceptionsAnalyzer : DocumentedExceptionsAnalyzerBase
    {
        public const string DiagnosticId = "Ex0103";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: "Implicit constructor may throw undocumented exception",
            messageFormat: "Implicit constructor of '{0}' may throw undocumented exception: {1}",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

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

                var contextCache = new ConcurrentDictionary<AnalyzerConfigOptions, Context>();

                compilationStartContext.RegisterSymbolAction(symbolContext => AnalyzeSymbol(symbolContext, documentedExceptionTypesProvider, contextCache), SymbolKind.NamedType);
            });
        }

        private static void AnalyzeSymbol(
            SymbolAnalysisContext symbolContext,
            DocumentedExceptionTypesProvider documentedExceptionTypesProvider,
            ConcurrentDictionary<AnalyzerConfigOptions, Context> contextCache)
        {
            var symbol = (INamedTypeSymbol)symbolContext.Symbol;
            var cancellationToken = symbolContext.CancellationToken;

            if (symbol.TypeKind != TypeKind.Class)
                return;

            foreach (var constructor in symbol.InstanceConstructors)
                if (!constructor.IsImplicitlyDeclared)
                    return;

            var baseType = symbol.BaseType;
            if (baseType == null)
                return;

            var baseConstructor = baseType.GetParameterlessConstructor();
            if (baseConstructor == null)
                return;

            var syntaxReferences = symbol.DeclaringSyntaxReferences;
            if (syntaxReferences.Length == 0)
                return;
            var syntaxReference = syntaxReferences[0];
            var syntaxTree = syntaxReference.SyntaxTree;

            var options = symbolContext.Options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
            var context = GetOrCreateContext(contextCache, options, documentedExceptionTypesProvider);

            var documentedExceptionTypes = documentedExceptionTypesProvider.GetDocumentedExceptionTypes(baseConstructor, cancellationToken);

            ExceptionTypesBuilder? builder = null;

            foreach (var documentedExceptionType in documentedExceptionTypes)
            {
                if (!documentedExceptionType.ExceptionType.HasBaseConversionTo(context.UncheckedExceptionTypes))
                {
                    if (builder == null)
                        builder = ExceptionTypesBuilder.Allocate();

                    builder.Add(documentedExceptionType.ExceptionType);
                }
            }

            if (builder != null)
            {
                var undocumentedExceptionTypes = builder.ToImmutable();
                builder.Clear();

                var syntaxNode = (ClassDeclarationSyntax)syntaxReference.GetSyntax(cancellationToken);
                var location = syntaxNode.Identifier.GetLocation();

                ReportDiagnostic(symbolContext, location, symbol, baseConstructor, undocumentedExceptionTypes);

                builder.Free();
            }
        }

        private static void ReportDiagnostic(SymbolAnalysisContext symbolContext, Location location, INamedTypeSymbol symbol, IMethodSymbol throwerSymbol, ImmutableArray<INamedTypeSymbol> exceptionTypes)
        {
            string exceptionTypeIds = string.Join(",", exceptionTypes.Select(x => x.OriginalDefinition.GetDocumentationCommentId()));

            var builder = ImmutableDictionary.CreateBuilder<string, string>();

            builder.Add(PropertyKeys.ExceptionTypeIds, exceptionTypeIds);

            builder.Add(PropertyKeys.ClassId, symbol.OriginalDefinition.GetDocumentationCommentId());

            builder.Add(PropertyKeys.ThrowerMemberId, throwerSymbol.OriginalDefinition.GetDocumentationCommentId());

            var properties = builder.ToImmutable();

            string symbolName = symbol.ToDisplayString(ConstructorDiagnosticDisplayFormat);

            string exceptionNames = string.Join(", ", exceptionTypes.Select(x => x.ToDisplayString(TypeDiagnosticDisplayFormat)));

            symbolContext.ReportDiagnostic(Diagnostic.Create(
                descriptor: Rule,
                location: location,
                properties: properties,
                messageArgs: new[] { symbolName, exceptionNames }));
        }

        internal static class PropertyKeys
        {
            public const string ExceptionTypeIds = nameof(ExceptionTypeIds);

            public const string ClassId = nameof(ClassId);

            public const string ThrowerMemberId = nameof(ThrowerMemberId);
        }
    }
}
