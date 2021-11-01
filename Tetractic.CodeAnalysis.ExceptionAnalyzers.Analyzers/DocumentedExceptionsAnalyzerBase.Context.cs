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
    public partial class DocumentedExceptionsAnalyzerBase
    {
        internal sealed class Context
        {
            public Context(
                DocumentedExceptionTypesProvider documentedExceptionTypesProvider,
                ImmutableArray<INamedTypeSymbol> ignoredExceptionTypes,
                ImmutableArray<INamedTypeSymbol> intransitiveExceptionTypesPublic,
                ImmutableArray<INamedTypeSymbol> intransitiveExceptionTypesPrivate,
                ImmutableArray<INamedTypeSymbol> intransitiveExceptionTypesInternal)
            {
                DocumentedExceptionTypesProvider = documentedExceptionTypesProvider;
                IgnoredExceptionTypes = ignoredExceptionTypes;
                IntransitiveExceptionTypesPublic = intransitiveExceptionTypesPublic;
                IntransitiveExceptionTypesPrivate = intransitiveExceptionTypesPrivate;
                IntransitiveExceptionTypesInternal = intransitiveExceptionTypesInternal;
            }

            public Compilation Compilation => DocumentedExceptionTypesProvider.Compilation;

            public DocumentedExceptionTypesProvider DocumentedExceptionTypesProvider { get; }

            public ImmutableArray<INamedTypeSymbol> IgnoredExceptionTypes { get; }

            public ImmutableArray<INamedTypeSymbol> IntransitiveExceptionTypesPublic { get; }

            public ImmutableArray<INamedTypeSymbol> IntransitiveExceptionTypesPrivate { get; }

            public ImmutableArray<INamedTypeSymbol> IntransitiveExceptionTypesInternal { get; }
        }
    }
}
