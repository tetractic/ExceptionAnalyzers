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

        public static ImmutableArray<MemberExceptionAdjustment> ApplyAdjustments(ImmutableArray<MemberExceptionAdjustment> existingAdjustments, ImmutableArray<MemberExceptionAdjustment> newAdjustments)
        {
            var builder = ImmutableArray.CreateBuilder<MemberExceptionAdjustment>(existingAdjustments.Length + newAdjustments.Length);

            foreach (var existingAdjustment in existingAdjustments)
            {
                bool keep = true;

                foreach (var newAdjustment in newAdjustments)
                {
                    // Discard existing adjustment if new adjustment overrides it.
                    if (((existingAdjustment.Kind == ExceptionAdjustmentKind.Addition &&
                          newAdjustment.Kind == ExceptionAdjustmentKind.Removal) ||
                         (existingAdjustment.Kind == ExceptionAdjustmentKind.Removal &&
                          newAdjustment.Kind == ExceptionAdjustmentKind.Addition)) &&
                        (newAdjustment.Accessor == null ||
                         existingAdjustment.Accessor == newAdjustment.Accessor) &&
                        existingAdjustment.Flag == newAdjustment.Flag &&
                        existingAdjustment.ExceptionTypeId == newAdjustment.ExceptionTypeId)
                    {
                        keep = false;
                        break;
                    }
                }

                if (keep)
                    builder.Add(existingAdjustment);
            }

            builder.AddRange(newAdjustments);

            return builder.Count == builder.Capacity
                ? builder.MoveToImmutable()
                : builder.ToImmutable();
        }

        public static bool ApplyFlagAdjustments(bool result, ImmutableArray<MemberExceptionAdjustment> adjustments, AccessorKind accessorKind, string flag, INamedTypeSymbol exceptionType)
        {
            string? exceptionTypeId = exceptionType.GetDeclarationDocumentationCommentId();

            result = ApplyFlagAdjustmentsCore(result, adjustments, AccessorKind.Unspecified, flag, exceptionTypeId);

            if (accessorKind != AccessorKind.Unspecified)
                result = ApplyFlagAdjustmentsCore(result, adjustments, accessorKind, flag, exceptionTypeId);

            return result;

            static bool ApplyFlagAdjustmentsCore(bool result, ImmutableArray<MemberExceptionAdjustment> adjustments, AccessorKind accessorKind, string flag, string? exceptionTypeId)
            {
                string? accessor = DocumentedExceptionType.GetAccessorName(accessorKind);

                if (result)
                {
                    foreach (var adjustment in adjustments)
                    {
                        if (adjustment.Kind == ExceptionAdjustmentKind.Removal &&
                            adjustment.Accessor == accessor &&
                            adjustment.Flag == flag &&
                            adjustment.ExceptionTypeId == exceptionTypeId)
                        {
                            result = false;
                            break;
                        }
                    }
                }

                if (!result)
                {
                    foreach (var adjustment in adjustments)
                    {
                        if (adjustment.Kind == ExceptionAdjustmentKind.Addition &&
                            adjustment.Accessor == accessor &&
                            adjustment.Flag == flag &&
                            adjustment.ExceptionTypeId == exceptionTypeId)
                        {
                            result = true;
                            break;
                        }
                    }
                }

                return result;
            }
        }

        private static ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> GetGlobalAdjustments()
        {
            using (var stream = typeof(ExceptionAdjustments).Assembly.GetManifestResourceStream("Tetractic.CodeAnalysis.ExceptionAnalyzers.GlobalExceptionAdjustments.txt"))
            {
                var text = SourceText.From(stream);

                var adjustmentsFile = ExceptionAdjustmentsFile.Load(text);

                Debug.Assert(adjustmentsFile.Diagnostics.IsEmpty);

                return adjustmentsFile.MemberAdjustments;
            }
        }
    }
}
