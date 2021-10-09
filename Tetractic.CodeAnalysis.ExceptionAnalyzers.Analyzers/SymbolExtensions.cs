// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal static class SymbolExtensions
    {
        public static Location GetFirstLocationOrNone(this ISymbol symbol)
        {
            var locations = symbol.Locations;
            return locations.Length > 0 ? locations[0] : Location.None;
        }

        public static IMethodSymbol? GetParameterlessConstructor(this INamedTypeSymbol type)
        {
            foreach (var constructor in type.InstanceConstructors)
                if (constructor.Parameters.IsEmpty)
                    return constructor;

            return null;
        }
    }
}
