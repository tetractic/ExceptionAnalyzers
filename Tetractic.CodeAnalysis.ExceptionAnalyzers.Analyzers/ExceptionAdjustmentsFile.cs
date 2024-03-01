// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
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
            var text = file.GetText(cancellationToken);
            if (text is null)
                return new ExceptionAdjustmentsFile(ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>>.Empty, ImmutableArray<Diagnostic>.Empty);

            return _cache.GetValue(file, _ => Load(text, file.Path));
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

                List<MemberExceptionAdjustment>? adjustments;
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
            int nextStart;
            int lookAhead;

            int symbolIdStart = 0;
            int symbolIdEnd;
            if (!LexNonspace(line, symbolIdStart, out symbolIdEnd) ||
                symbolIdEnd == symbolIdStart)
            {
                var span = new TextSpan(lineSpan.Start + symbolIdStart, 0);
                reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedIdentifierRule, span);
                goto fail;
            }

            if (!LexSpace(line, symbolIdEnd, out nextStart, out lookAhead))
            {
                var span = new TextSpan(lineSpan.Start + symbolIdEnd, nextStart - symbolIdEnd);
                reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedSpaceRule, span);
                goto fail;
            }

            int accessorStart;
            int accessorEnd;
            if (lookAhead != '-' && lookAhead != '+' && lookAhead != '$')
            {
                accessorStart = nextStart;
                if (!LexNonspace(line, accessorStart, out accessorEnd) ||
                    accessorEnd == accessorStart)
                {
                    var span = new TextSpan(lineSpan.Start + accessorStart, 0);
                    reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedAccessorOrOperatorRule, span);
                    goto fail;
                }

                if (!LexSpace(line, accessorEnd, out nextStart, out lookAhead))
                {
                    var span = new TextSpan(lineSpan.Start + accessorEnd, nextStart - accessorEnd);
                    reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedSpaceRule, span);
                    goto fail;
                }
            }
            else
            {
                accessorStart = 0;
                accessorEnd = 0;
            }

            int flagStart;
            int flagEnd;
            if (lookAhead == '$')
            {
                flagStart = nextStart + 1;
                if (!LexNonspace(line, flagStart, out flagEnd) ||
                    flagEnd == flagStart)
                {
                    var span = new TextSpan(lineSpan.Start + flagStart, 0);
                    reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedIdentifierRule, span);
                    goto fail;
                }

                if (!LexSpace(line, flagEnd, out nextStart, out lookAhead))
                {
                    var span = new TextSpan(lineSpan.Start + flagEnd, nextStart - flagEnd);
                    reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedSpaceRule, span);
                    goto fail;
                }
            }
            else
            {
                flagStart = 0;
                flagEnd = 0;
            }

            ExceptionAdjustmentKind adjustmentKind;
            switch (lookAhead)
            {
                case '-':
                    adjustmentKind = ExceptionAdjustmentKind.Removal;
                    break;
                case '+':
                    adjustmentKind = ExceptionAdjustmentKind.Addition;
                    break;
                default:
                {
                    var span = new TextSpan(lineSpan.Start + nextStart, lookAhead == -1 ? 0 : 1);
                    reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedOperatorRule, span);
                    goto fail;
                }
            }

            int exceptionTypeIdStart = nextStart + 1;
            int exceptionTypeIdEnd;
            if (!LexNonspace(line, exceptionTypeIdStart, out exceptionTypeIdEnd) ||
                exceptionTypeIdEnd == exceptionTypeIdStart)
            {
                var span = new TextSpan(lineSpan.Start + exceptionTypeIdStart, 0);
                reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedIdentifierRule, span);
                goto fail;
            }

            if (exceptionTypeIdEnd != line.Length)
            {
                var span = new TextSpan(lineSpan.Start + exceptionTypeIdEnd, line.Length - exceptionTypeIdEnd);
                reportDiagnostic(ExceptionAdjustmentsFileAnalyzer.ExpectedEolRule, span);
                goto fail;
            }

            var symbolIdSpan = new TextSpan(lineSpan.Start + symbolIdStart, symbolIdEnd - symbolIdStart);
            symbolId = line.Substring(symbolIdStart, symbolIdEnd - symbolIdStart);

            var accessorSpan = new TextSpan(lineSpan.Start + accessorStart, accessorEnd - accessorStart);
            string? accessor = accessorSpan.IsEmpty ? null : line.Substring(accessorStart, accessorEnd - accessorStart);

            var flagSpan = new TextSpan(lineSpan.Start + flagStart, flagEnd - flagStart);
            string? flag = flagSpan.IsEmpty ? null : line.Substring(flagStart, flagEnd - flagStart);

            var exceptionTypeIdSpan = new TextSpan(lineSpan.Start + exceptionTypeIdStart, exceptionTypeIdEnd - exceptionTypeIdStart);
            string exceptionTypeId = line.Substring(exceptionTypeIdStart, exceptionTypeIdEnd - exceptionTypeIdStart);

            adjustment = new MemberExceptionAdjustment(
                accessor: accessor,
                flag: flag,
                kind: adjustmentKind,
                exceptionTypeId: exceptionTypeId,
                symbolIdSpan: symbolIdSpan,
                accessorSpan: accessorSpan,
                flagSpan: flagSpan,
                exceptionTypeIdSpan: exceptionTypeIdSpan);
            return true;

        fail:
            symbolId = default;
            adjustment = default;
            return false;

            static bool LexNonspace(string line, int start, out int end)
            {
                end = line.IndexOf(' ', start);
                if (end != -1)
                {
                    return true;
                }
                else if (start != line.Length)
                {
                    end = line.Length;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            static bool LexSpace(string line, int start, out int end, out int lookAhead)
            {
                if (start == line.Length)
                {
                    lookAhead = -1;
                    end = start;
                    return false;
                }
                else if (line[start] != ' ')
                {
                    lookAhead = -1;
                    end = start + 1;
                    return false;
                }
                else
                {
                    lookAhead = start < line.Length - 1 ? line[start + 1] : -1;
                    end = start + 1;
                    return true;
                }
            }
        }
    }
}
