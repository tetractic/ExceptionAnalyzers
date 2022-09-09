// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal static class INamedTypeSymbolExtensions
    {
        public static bool HasBaseConversionTo(this INamedTypeSymbol type, INamedTypeSymbol otherType)
        {
            if (type.TypeKind == TypeKind.Class &&
                otherType.TypeKind == TypeKind.Class)
            {
                for (INamedTypeSymbol? tempType = type; tempType != null; tempType = tempType.BaseType)
                    if (SymbolEqualityComparer.Default.Equals(tempType, otherType))
                        return true;
            }

            return false;
        }

        public static bool HasBaseConversionTo(this INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol> otherTypes)
        {
            foreach (var otherType in otherTypes)
                if (SymbolEqualityComparer.Default.Equals(type, otherType))
                    return true;

            foreach (var otherType in otherTypes)
                if (HasBaseConversionTo(type, otherType))
                    return true;

            return false;
        }
    }
}
