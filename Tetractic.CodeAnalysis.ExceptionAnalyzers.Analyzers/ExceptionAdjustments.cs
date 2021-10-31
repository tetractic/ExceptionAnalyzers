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
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal static class ExceptionAdjustments
    {
        public const string DefaultFileName = "ExceptionAdjustments.txt";

        private const string _filenamePrefix = "ExceptionAdjustments";

        private const string _filenameSuffix = ".txt";

        public static readonly ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> Global = GetGlobalAdjustments();

        public static ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> Load(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
        {
            var adjustmentFiles = ImmutableArray<ExceptionAdjustmentsFile>.Empty;

            foreach (var additionalFile in additionalFiles)
            {
                if (!IsFileName(Path.GetFileName(additionalFile.Path)))
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

        public static bool IsFileName(string fileName)
        {
            return fileName.StartsWith(_filenamePrefix, StringComparison.Ordinal) && fileName.EndsWith(_filenameSuffix, StringComparison.Ordinal);
        }

        public static ImmutableArray<DocumentedExceptionType> ApplyAdjustments(ImmutableArray<DocumentedExceptionType> exceptionTypes, ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> adjustments, ISymbol symbol, Compilation compilation)
        {
            string? symbolId = symbol.GetDeclarationDocumentationCommentId();

            if (symbolId != null &&
                adjustments.TryGetValue(symbolId, out var symbolAdjustments))
            {
                var builder = DocumentedExceptionTypesBuilder.Allocate();

                foreach (var exceptionType in exceptionTypes)
                    builder.Add(exceptionType);

                ApplyAdjustments(builder, symbolAdjustments, symbol, compilation);

                exceptionTypes = builder.ToImmutable();
                builder.Free();
            }

            return exceptionTypes;
        }

        public static void ApplyAdjustments(DocumentedExceptionTypesBuilder exceptionTypesBuilder, ImmutableArray<MemberExceptionAdjustment> symbolAdjustments, ISymbol symbol, Compilation compilation)
        {
            ApplyAdjustments(exceptionTypesBuilder, symbolAdjustments, symbol, unspecifiedAccessor: true, compilation);

            ApplyAdjustments(exceptionTypesBuilder, symbolAdjustments, symbol, unspecifiedAccessor: false, compilation);

            static void ApplyAdjustments(DocumentedExceptionTypesBuilder builder, ImmutableArray<MemberExceptionAdjustment> symbolAdjustments, ISymbol symbol, bool unspecifiedAccessor, Compilation compilation)
            {
                foreach (var adjustment in symbolAdjustments)
                {
                    if (adjustment.Flag != null)
                        continue;
                    if ((adjustment.Accessor == null) != unspecifiedAccessor)
                        continue;
                    if (adjustment.Kind != ExceptionAdjustmentKind.Removal)
                        continue;
                    if (!DocumentedExceptionType.TryGetAccessorKind(adjustment.Accessor, out var accessorKind))
                        continue;

                    var exceptionTypeSymbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(adjustment.ExceptionTypeId, compilation);
                    if (exceptionTypeSymbol is INamedTypeSymbol exceptionType)
                        builder.Remove(symbol, exceptionType, accessorKind);
                }

                foreach (var adjustment in symbolAdjustments)
                {
                    if (adjustment.Flag != null)
                        continue;
                    if ((adjustment.Accessor == null) != unspecifiedAccessor)
                        continue;
                    if (adjustment.Kind != ExceptionAdjustmentKind.Addition)
                        continue;
                    if (!DocumentedExceptionType.TryGetAccessorKind(adjustment.Accessor, out var accessorKind))
                        continue;

                    var exceptionTypeSymbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(adjustment.ExceptionTypeId, compilation);
                    if (exceptionTypeSymbol is INamedTypeSymbol exceptionType)
                        builder.Add(symbol, exceptionType, accessorKind);
                }
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
