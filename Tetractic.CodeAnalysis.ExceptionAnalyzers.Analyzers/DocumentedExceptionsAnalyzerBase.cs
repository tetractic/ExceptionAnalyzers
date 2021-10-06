// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    public abstract partial class DocumentedExceptionsAnalyzerBase : DiagnosticAnalyzer
    {
        protected static readonly SymbolDisplayFormat ConstructorDiagnosticDisplayFormat = new SymbolDisplayFormat(
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.IncludeModifiers | SymbolDisplayMemberOptions.IncludeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        protected static readonly SymbolDisplayFormat MemberDiagnosticDisplayFormat = new SymbolDisplayFormat(
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        protected static readonly SymbolDisplayFormat TypeDiagnosticDisplayFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

        private static readonly ConditionalWeakTable<Compilation, DocumentedExceptionTypesProvider> _cache = new ConditionalWeakTable<Compilation, DocumentedExceptionTypesProvider>();

        private protected static DocumentedExceptionTypesProvider GetOrCreateDocumentedExceptionTypesProvider(Compilation compilation, ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> adjustments)
        {
            return _cache.GetValue(compilation, _ => new DocumentedExceptionTypesProvider(compilation, adjustments));
        }

        private protected static Context GetOrCreateContext(
            ConcurrentDictionary<AnalyzerConfigOptions, Context> contextCache,
            AnalyzerConfigOptions options,
            DocumentedExceptionTypesProvider documentedExceptionTypesProvider)
        {
            if (!contextCache.TryGetValue(options, out var context))
            {
                var compilation = documentedExceptionTypesProvider.Compilation;

                var uncheckedExceptionTypes = options.TryGetValue("dotnet_unchecked_exceptions", out string uncheckedExceptionNames)
                    ? GetTypeSymbolsByDeclarationId(compilation, uncheckedExceptionNames)
                    : GetDefaultUncheckedExceptionTypeSymbols(compilation);

                var intransitiveExceptionTypes = options.TryGetValue("dotnet_intransitive_exceptions", out string intransitiveExceptionTypeNames)
                    ? GetTypeSymbolsByDeclarationId(compilation, intransitiveExceptionTypeNames)
                    : GetDefaultIntransitiveExceptionTypeSymbols(compilation);

                context = contextCache.GetOrAdd(options, new Context(documentedExceptionTypesProvider, uncheckedExceptionTypes, intransitiveExceptionTypes));
            }

            return context;
        }

        private static ImmutableArray<INamedTypeSymbol> GetTypeSymbolsByDeclarationId(Compilation compilation, string delimitedTypeNames)
        {
            string[] typeNames = delimitedTypeNames.Split(' ');
            for (int i = 0; i < typeNames.Length; ++i)
                typeNames[i] = typeNames[i].Trim();

            return GetTypeSymbolsByDeclarationId(compilation, typeNames);
        }

        private static ImmutableArray<INamedTypeSymbol> GetTypeSymbolsByDeclarationId(Compilation compilation, string[] typeNames)
        {
            var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(typeNames.Length);
            foreach (string typeName in typeNames)
            {
                var typeSymbol = (INamedTypeSymbol)DocumentationCommentId.GetFirstSymbolForDeclarationId("T:" + typeName, compilation);
                if (typeSymbol != null)
                    builder.Add(typeSymbol);
            }
            return builder.Count == builder.Capacity
                ? builder.MoveToImmutable()
                : builder.ToImmutable();
        }

        private static ImmutableArray<INamedTypeSymbol> GetDefaultUncheckedExceptionTypeSymbols(Compilation compilation)
        {
            return GetTypeSymbolsByDeclarationId(compilation, new[]
            {
                "System.NullReferenceException",
                "System.UnreachableException",
            });
        }

        private static ImmutableArray<INamedTypeSymbol> GetDefaultIntransitiveExceptionTypeSymbols(Compilation compilation)
        {
            return GetTypeSymbolsByDeclarationId(compilation, new[]
            {
                "System.ArgumentException",
                "System.IndexOutOfRangeException",
                "System.InvalidOperationException",
                "System.Collections.Generic.KeyNotFoundException",
            });
        }
    }
}
