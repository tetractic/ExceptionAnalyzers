// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal sealed class ExceptionAdjustmentsFile
    {
        private static readonly ConditionalWeakTable<AdditionalText, ExceptionAdjustmentsFile> _cache = new ConditionalWeakTable<AdditionalText, ExceptionAdjustmentsFile>();

        public readonly ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> MemberAdjustments;

        public readonly ImmutableArray<Diagnostic> Diagnostics;

        public ExceptionAdjustmentsFile(ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> memberAdjustments, ImmutableArray<Diagnostic> diagnostics)
        {
            MemberAdjustments = memberAdjustments;
            Diagnostics = diagnostics;
        }

        public static ExceptionAdjustmentsFile Load(AdditionalText file, CancellationToken cancellationToken)
        {
            return _cache.GetValue(file, _ => Load(file.GetText(cancellationToken), file.Path));
        }

        public static ExceptionAdjustmentsFile Load(SourceText text, string filePath = null)
        {
            var diagnosticsBuilder = ImmutableArray.CreateBuilder<Diagnostic>();

            var symbolsEntries = new Dictionary<string, List<MemberExceptionAdjustment>>();

            foreach (var textLine in text.Lines)
            {
                if (textLine.Span.IsEmpty)
                    continue;

                string line = textLine.ToString();

                if (line[0] == '#')
                    continue;

                int symbolIdStart = 0;
                int symbolIdEnd = line.IndexOf(' ');
                if (symbolIdEnd == -1)
                {
                    var span = new TextSpan(textLine.Span.End, 0);
                    AddDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedSpaceRule, span);
                    continue;
                }
                if (symbolIdEnd == symbolIdStart)
                {
                    var span = new TextSpan(textLine.Span.Start + symbolIdStart, 0);
                    AddDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedIdentifierRule, span);
                    continue;
                }

                int adjustmentIndex = symbolIdEnd + 1;
                if (adjustmentIndex == line.Length)
                {
                    var span = new TextSpan(textLine.Span.End, 0);
                    AddDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedOperatorRule, span);
                    continue;
                }

                ExceptionAdjustmentKind adjustmentKind;
                switch (line[adjustmentIndex])
                {
                    case '-':
                        adjustmentKind = ExceptionAdjustmentKind.Removal;
                        break;
                    case '+':
                        adjustmentKind = ExceptionAdjustmentKind.Addition;
                        break;
                    default:
                        var span = new TextSpan(textLine.Span.Start + adjustmentIndex, 1);
                        AddDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedOperatorRule, span);
                        continue;
                }

                int accessorStart = adjustmentIndex + 1;
                int accessorEnd = line.IndexOf(' ', accessorStart);
                if (accessorEnd == -1)
                {
                    var span = new TextSpan(textLine.Span.End, 0);
                    AddDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedSpaceRule, span);
                    continue;
                }

                int exceptionTypeIdStart = accessorEnd + 1;
                int exceptionTypeIdEnd = line.Length;
                if (exceptionTypeIdEnd == exceptionTypeIdStart)
                {
                    var span = new TextSpan(textLine.Span.Start + exceptionTypeIdStart, 0);
                    AddDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedIdentifierRule, span);
                    continue;
                }

                var symbolIdSpan = new TextSpan(textLine.Span.Start + symbolIdStart, symbolIdEnd - symbolIdStart);
                var symbolIdLineSpan = text.Lines.GetLinePositionSpan(symbolIdSpan);
                string symbolId = line.Substring(symbolIdStart, symbolIdEnd - symbolIdStart);

                var accessorSpan = new TextSpan(textLine.Span.Start + accessorStart, accessorEnd - accessorStart);
                var accessorLineSpan = text.Lines.GetLinePositionSpan(accessorSpan);
                string accessor = accessorSpan.IsEmpty ? null : line.Substring(accessorStart, accessorEnd - accessorStart);

                var exceptionTypeIdSpan = new TextSpan(textLine.Span.Start + exceptionTypeIdStart, exceptionTypeIdEnd - exceptionTypeIdStart);
                var exceptionTypeIdLineSpan = text.Lines.GetLinePositionSpan(exceptionTypeIdSpan);
                string exceptionTypeId = line.Substring(exceptionTypeIdStart, exceptionTypeIdEnd - exceptionTypeIdStart);

                var entry = new MemberExceptionAdjustment(
                    accessor: accessor,
                    kind: adjustmentKind,
                    exceptionTypeId: exceptionTypeId,
                    symbolIdSpan: symbolIdSpan,
                    symbolIdLineSpan: symbolIdLineSpan,
                    accessorSpan: accessorSpan,
                    accessorLineSpan: accessorLineSpan,
                    exceptionTypeIdSpan: exceptionTypeIdSpan,
                    exceptionTypeIdLineSpan: exceptionTypeIdLineSpan);

                List<MemberExceptionAdjustment> symbolEntries;
                if (!symbolsEntries.TryGetValue(symbolId, out symbolEntries))
                {
                    symbolEntries = new List<MemberExceptionAdjustment>();
                    symbolsEntries.Add(symbolId, symbolEntries);
                }
                symbolEntries.Add(entry);
            }

            var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<MemberExceptionAdjustment>>();

            foreach (var symbolsEntry in symbolsEntries)
            {
                string symbolId = symbolsEntry.Key;
                var entries = symbolsEntry.Value.ToImmutableArray();

                builder.Add(symbolId, entries);
            }

            var memberAdjustments = builder.ToImmutable();

            var diagnostics = diagnosticsBuilder.ToImmutable();

            return new ExceptionAdjustmentsFile(memberAdjustments, diagnostics);

            void AddDiagnostic(DiagnosticDescriptor descriptor, TextSpan span)
            {
                var linePositionSpan = text.Lines.GetLinePositionSpan(span);
                diagnosticsBuilder.Add(Diagnostic.Create(
                    descriptor: descriptor,
                    location: filePath is null ? Location.None : Location.Create(filePath, span, linePositionSpan)));
            }
        }
    }
}
