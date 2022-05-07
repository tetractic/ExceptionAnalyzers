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
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.IO;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ExceptionAdjustmentsFileAnalyzer : DiagnosticAnalyzer
    {
        public const string SyntaxErrorDiagnosticId = "Ex0001";

        internal static readonly DiagnosticDescriptor SyntaxErrorRule = new DiagnosticDescriptor(
            id: SyntaxErrorDiagnosticId,
            title: "Exception adjustments syntax error",
            messageFormat: "Syntax error",
            category: "Analysis",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor ExpectedSpaceRule = new DiagnosticDescriptor(
            id: SyntaxErrorDiagnosticId,
            title: "Exception adjustments syntax error",
            messageFormat: "Expected ' '",
            category: "Analysis",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor ExpectedIdentifierRule = new DiagnosticDescriptor(
            id: SyntaxErrorDiagnosticId,
            title: "Exception adjustments syntax error",
            messageFormat: "Expected identifier",
            category: "Analysis",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor ExpectedAccessorOrOperatorRule = new DiagnosticDescriptor(
            id: SyntaxErrorDiagnosticId,
            title: "Exception adjustments syntax error",
            messageFormat: "Expected accessor or '-' or '+'",
            category: "Analysis",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor ExpectedOperatorRule = new DiagnosticDescriptor(
            id: SyntaxErrorDiagnosticId,
            title: "Exception adjustments syntax error",
            messageFormat: "Expected '-' or '+'",
            category: "Analysis",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor ExpectedEolRule = new DiagnosticDescriptor(
            id: SyntaxErrorDiagnosticId,
            title: "Exception adjustments syntax error",
            messageFormat: "Expected end-of-line",
            category: "Analysis",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public const string SymbolDiagnosticId = "Ex0002";

        internal static readonly DiagnosticDescriptor SymbolRule = new DiagnosticDescriptor(
            id: SymbolDiagnosticId,
            title: "Exception adjustments syntax error",
            messageFormat: "Symbol does not exist or identifier is invalid",
            category: "Analysis",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            SyntaxErrorRule,
            SymbolRule,
        });

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // TODO: RegisterAdditionalFileAction
            context.RegisterCompilationAction(compilationContext =>
            {
                var compilation = compilationContext.Compilation;
                var additionalFiles = compilationContext.Options.AdditionalFiles;
                var cancellationToken = compilationContext.CancellationToken;

                foreach (var additionalFile in additionalFiles)
                {
                    if (!ExceptionAdjustments.IsFileName(Path.GetFileName(additionalFile.Path)))
                        continue;

                    var adjustmentsFile = ExceptionAdjustmentsFile.Load(additionalFile, cancellationToken);

                    foreach (var diagnostic in adjustmentsFile.Diagnostics)
                        compilationContext.ReportDiagnostic(diagnostic);

                    SourceText? text = null;

                    foreach (var adjustments in adjustmentsFile.MemberAdjustments)
                    {
                        var symbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(adjustments.Key, compilation);

                        foreach (var adjustment in adjustments.Value)
                        {
                            if (symbol is null)
                            {
                                text ??= additionalFile.GetText(cancellationToken);

                                var symbolIdLineSpan = text.Lines.GetLinePositionSpan(adjustment.SymbolIdSpan);

                                compilationContext.ReportDiagnostic(Diagnostic.Create(
                                    descriptor: SymbolRule,
                                    location: Location.Create(additionalFile.Path, adjustment.SymbolIdSpan, symbolIdLineSpan)));
                            }

                            var exceptionType = DocumentationCommentId.GetFirstSymbolForDeclarationId(adjustment.ExceptionTypeId, compilation);
                            if (exceptionType is null)
                            {
                                text ??= additionalFile.GetText(cancellationToken);

                                var exceptionTypeIdLineSpan = text.Lines.GetLinePositionSpan(adjustment.ExceptionTypeIdSpan);

                                compilationContext.ReportDiagnostic(Diagnostic.Create(
                                    descriptor: SymbolRule,
                                    location: Location.Create(additionalFile.Path, adjustment.ExceptionTypeIdSpan, exceptionTypeIdLineSpan)));
                            }
                        }
                    }
                }
            });
        }
    }
}
