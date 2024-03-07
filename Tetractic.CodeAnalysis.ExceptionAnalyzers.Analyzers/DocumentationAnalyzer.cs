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

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DocumentationAnalyzer : DiagnosticAnalyzer
    {
        public const string DocumentationModeDiagnosticId = "Ex0000";

        internal static readonly DiagnosticDescriptor DocumentationModeRule = new DiagnosticDescriptor(
            id: DocumentationModeDiagnosticId,
            title: "Documentation comments parsing is disabled",
            messageFormat: "The compiler did not parse documentation comments, which are required for exception analysis",
            category: "Analysis",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            DocumentationModeRule,
        });

        public override void Initialize(AnalysisContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationAction(compilationContext =>
            {
                foreach (var syntaxTree in compilationContext.Compilation.SyntaxTrees)
                {
                    if (syntaxTree.Options.DocumentationMode == DocumentationMode.None)
                    {
                        compilationContext.ReportDiagnostic(Diagnostic.Create(
                            descriptor: DocumentationModeRule,
                            location: null));
                        break;
                    }
                }
            });
        }
    }
}
