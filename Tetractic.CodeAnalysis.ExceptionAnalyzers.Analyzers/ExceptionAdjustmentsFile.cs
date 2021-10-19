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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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

        public static ExceptionAdjustmentsFile Load(SourceText text, string? filePath = null)
        {
            var diagnosticsBuilder = ImmutableArray.CreateBuilder<Diagnostic>();

            var mutableBuilder = new Dictionary<string, List<MemberExceptionAdjustment>>();

            foreach (var textLine in text.Lines)
            {
                if (textLine.Span.IsEmpty)
                    continue;

                string line = textLine.ToString();

                if (line[0] == '#')
                    continue;

                string? symbolId;
                MemberExceptionAdjustment adjustment;
                if (!TryParseAdjustment(line, textLine.Span, ReportDiagnostic, out symbolId, out adjustment))
                    continue;

                List<MemberExceptionAdjustment> adjustments;
                if (!mutableBuilder.TryGetValue(symbolId, out adjustments))
                {
                    adjustments = new List<MemberExceptionAdjustment>();
                    mutableBuilder.Add(symbolId, adjustments);
                }
                adjustments.Add(adjustment);
            }

            var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<MemberExceptionAdjustment>>();

            foreach (var entry in mutableBuilder)
                builder.Add(entry.Key, entry.Value.ToImmutableArray());

            var memberAdjustments = builder.ToImmutable();

            var diagnostics = diagnosticsBuilder.ToImmutable();

            return new ExceptionAdjustmentsFile(memberAdjustments, diagnostics);

            void ReportDiagnostic(DiagnosticDescriptor descriptor, TextSpan span)
            {
                var linePositionSpan = text.Lines.GetLinePositionSpan(span);
                diagnosticsBuilder.Add(Diagnostic.Create(
                    descriptor: descriptor,
                    location: filePath is null ? Location.None : Location.Create(filePath, span, linePositionSpan)));
            }
        }

        public static bool TryParseAdjustment(string line, TextSpan lineSpan, Action<DiagnosticDescriptor, TextSpan> reportDiagnostic, [NotNullWhen(true)] out string? symbolId, out MemberExceptionAdjustment adjustment)
        {
            if (line.Length == 0)
            {
                var span = new TextSpan(lineSpan.Start, 0);
                reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedIdentifierRule, span);
                goto fail;
            }

            int symbolIdStart = 0;
            int symbolIdEnd = line.IndexOf(' ');
            if (symbolIdEnd == -1)
            {
                var span = new TextSpan(lineSpan.End, 0);
                reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedSpaceRule, span);
                goto fail;
            }
            if (symbolIdEnd == symbolIdStart)
            {
                var span = new TextSpan(lineSpan.Start + symbolIdStart, 0);
                reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedIdentifierRule, span);
                goto fail;
            }

            int accessorStart = symbolIdEnd + 1;
            int accessorEnd = line.IndexOf(' ', accessorStart);
            if (accessorEnd == -1)
            {
                accessorStart = symbolIdEnd;
                accessorEnd = accessorStart;
            }

            int adjustmentIndex = accessorEnd + 1;
            if (adjustmentIndex == line.Length)
            {
                var span = new TextSpan(lineSpan.End, 0);
                reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedOperatorRule, span);
                goto fail;
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
                    var span = new TextSpan(lineSpan.Start + adjustmentIndex, 1);
                    reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedOperatorRule, span);
                    goto fail;
            }

            int exceptionTypeIdStart = adjustmentIndex + 1;
            int exceptionTypeIdEnd = line.Length;
            if (exceptionTypeIdEnd == exceptionTypeIdStart)
            {
                var span = new TextSpan(lineSpan.Start + exceptionTypeIdStart, 0);
                reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedIdentifierRule, span);
                goto fail;
            }

            var symbolIdSpan = new TextSpan(lineSpan.Start + symbolIdStart, symbolIdEnd - symbolIdStart);
            symbolId = line.Substring(symbolIdStart, symbolIdEnd - symbolIdStart);

            var accessorSpan = new TextSpan(lineSpan.Start + accessorStart, accessorEnd - accessorStart);
            string? accessor = accessorSpan.IsEmpty ? null : line.Substring(accessorStart, accessorEnd - accessorStart);

            var exceptionTypeIdSpan = new TextSpan(lineSpan.Start + exceptionTypeIdStart, exceptionTypeIdEnd - exceptionTypeIdStart);
            string exceptionTypeId = line.Substring(exceptionTypeIdStart, exceptionTypeIdEnd - exceptionTypeIdStart);

            adjustment = new MemberExceptionAdjustment(
                accessor: accessor,
                kind: adjustmentKind,
                exceptionTypeId: exceptionTypeId,
                symbolIdSpan: symbolIdSpan,
                accessorSpan: accessorSpan,
                exceptionTypeIdSpan: exceptionTypeIdSpan);
            return true;

        fail:
            symbolId = default;
            adjustment = default;
            return false;
        }
    }
}
