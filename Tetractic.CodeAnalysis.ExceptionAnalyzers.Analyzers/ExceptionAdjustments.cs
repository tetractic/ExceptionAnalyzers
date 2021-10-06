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
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal static class ExceptionAdjustments
    {
        public const string FileName = "ExceptionAdjustments.txt";

        public static readonly ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> Global = GetGlobalAdjustments();

        public static ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> Load(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
        {
            var adjustmentFiles = ImmutableArray<ExceptionAdjustmentsFile>.Empty;

            foreach (var additionalFile in additionalFiles)
            {
                if (Path.GetFileName(additionalFile.Path) != FileName)
                    continue;

                adjustmentFiles = adjustmentFiles.Add(ExceptionAdjustmentsFile.Load(additionalFile, cancellationToken));
            }

            if (adjustmentFiles.Length == 0)
            {
                return ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>>.Empty;
            }
            else if (adjustmentFiles.Length == 1)
            {
                return adjustmentFiles[0].MemberAdjustments;
            }
            else
            {
                var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<MemberExceptionAdjustment>>();

                foreach (var adjustmentFile in adjustmentFiles)
                {
                    foreach (var entry in adjustmentFile.MemberAdjustments)
                    {
                        if (builder.TryGetValue(entry.Key, out var adjustments))
                        {
                            adjustments = adjustments.AddRange(entry.Value);
                        }
                        else
                        {
                            adjustments = entry.Value;
                        }
                        builder[entry.Key] = adjustments;
                    }
                }

                return builder.ToImmutable();
            }
        }

        private static ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> GetGlobalAdjustments()
        {
            using (var stream = typeof(ExceptionAdjustments).Assembly.GetManifestResourceStream("Tetractic.CodeAnalysis.ExceptionAnalyzers.GlobalExceptionAdjustments.txt"))
            {
                var text = SourceText.From(stream);

                var exceptionAdjustmentsFile = ExceptionAdjustmentsFile.Load(text);

                Debug.Assert(exceptionAdjustmentsFile.Diagnostics.IsEmpty);

                return exceptionAdjustmentsFile.MemberAdjustments;
            }
        }
    }
}
