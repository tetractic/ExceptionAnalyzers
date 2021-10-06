// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal readonly struct DocumentedExceptionType
    {
        public DocumentedExceptionType(INamedTypeSymbol exceptionType, AccessorKind accessorKind)
        {
            ExceptionType = exceptionType;
            AccessorKind = accessorKind;
        }

        public INamedTypeSymbol ExceptionType { get; }

        public AccessorKind AccessorKind { get; }

        /// <summary>
        /// Returns a value indicating whether the exception type is subsumed by any exception type
        /// in a specified collection.
        /// </summary>
        /// <param name="otherDocumentedExceptionTypes">A collection of exception types.</param>
        /// <param name="compilation">The compilation.</param>
        /// <returns><see langword="true"/> if the documented exception type was subsumed by any of
        ///     the documented exceptions types.</returns>
        public bool IsSubsumedBy(ImmutableArray<DocumentedExceptionType> otherDocumentedExceptionTypes)
        {
            foreach (var otherDocumentedExceptionType in otherDocumentedExceptionTypes)
            {
                if (AccessorKind == otherDocumentedExceptionType.AccessorKind &&
                    ExceptionType.HasBaseConversionTo(otherDocumentedExceptionType.ExceptionType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
