// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.


using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Xml;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal sealed partial class DocumentedExceptionTypesProvider
    {
        private static readonly XmlReaderSettings _xmlReaderSettings = new XmlReaderSettings()
        {
            ConformanceLevel = ConformanceLevel.Fragment,
        };

        private readonly ConcurrentDictionary<ISymbol, ImmutableArray<DocumentedExceptionType>> _cache;

        private readonly ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> _adjustments;

        private ImmutableDictionary<ISymbol, AdjustmentsInfo>? _lazyAdjustmentsCache;

        public DocumentedExceptionTypesProvider(Compilation compilation, ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> adjustments)
        {
            Compilation = compilation;
            _adjustments = adjustments;
            _cache = new ConcurrentDictionary<ISymbol, ImmutableArray<DocumentedExceptionType>>(SymbolEqualityComparer.Default);
        }

        public Compilation Compilation { get; }

        public ImmutableArray<DocumentedExceptionType> GetDocumentedExceptionTypes(ISymbol symbol, CancellationToken cancellationToken)
        {
            SymbolStack? symbolStack = null;
            return GetDocumentedExceptionTypes(symbol, ref symbolStack, cancellationToken);
        }

        public bool TryGetDocumentedExceptionTypes(ISymbol symbol, out ImmutableArray<DocumentedExceptionType> exceptionTypes, CancellationToken cancellationToken)
        {
            SymbolStack? symbolStack = null;
            return TryGetDocumentedExceptionTypes(symbol, out exceptionTypes, ref symbolStack, cancellationToken);
        }

        public bool TryGetExceptionAdjustments(ISymbol symbol, out ImmutableArray<MemberExceptionAdjustment> adjustments, CancellationToken cancellationToken)
        {
            AdjustmentsInfo adustmentInfo;
            if (TryGetExceptionAdjustments(symbol, out adustmentInfo, cancellationToken))
            {
                adjustments = adustmentInfo.Adjustments;
                return true;
            }

            adjustments = default;
            return false;
        }

        private static bool XmlEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private ImmutableArray<DocumentedExceptionType> GetDocumentedExceptionTypes(ISymbol symbol, [NotNullIfNotNull("symbolStack")] ref SymbolStack? symbolStack, CancellationToken cancellationToken)
        {
            var result = GetDocumentedExceptionTypesOrDefault(symbol, ref symbolStack, cancellationToken);

            return result.IsDefault ? ImmutableArray<DocumentedExceptionType>.Empty : result;
        }

        private bool TryGetDocumentedExceptionTypes(ISymbol symbol, out ImmutableArray<DocumentedExceptionType> exceptionTypes, [NotNullIfNotNull("symbolStack")] ref SymbolStack? symbolStack, CancellationToken cancellationToken)
        {
            exceptionTypes = GetDocumentedExceptionTypesOrDefault(symbol, ref symbolStack, cancellationToken);

            return !exceptionTypes.IsDefault;
        }

        private ImmutableArray<DocumentedExceptionType> GetDocumentedExceptionTypesOrDefault(ISymbol symbol, [NotNullIfNotNull("symbolStack")] ref SymbolStack? symbolStack, CancellationToken cancellationToken)
        {
            symbol = symbol.GetDeclarationSymbol();

            if (!_cache.TryGetValue(symbol, out var result))
            {
                var builder = SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, Compilation.Assembly)
                    ? GetDocumentedExceptionTypesFromSyntax(symbol, ref symbolStack, cancellationToken)
                    : GetDocumentedExceptionTypesFromSymbol(symbol, ref symbolStack, cancellationToken);

                // A symbol is considered to be documented if documentation is missing but there are
                // exception adjustments on that symbol in the compilation.
                AdjustmentsInfo adjustmentInfo;
                if (TryGetExceptionAdjustments(symbol, out adjustmentInfo, cancellationToken) &&
                    !(builder == null && adjustmentInfo.HasCompilationAdjustments))
                {
                    if (builder == null)
                        builder = DocumentedExceptionTypesBuilder.Allocate();

                    ExceptionAdjustments.ApplyAdjustments(builder, adjustmentInfo.Adjustments, symbol, Compilation);
                }

                ImmutableArray<DocumentedExceptionType> exceptionTypes;
                if (builder != null)
                {
                    exceptionTypes = builder.ToImmutable();
                    builder.Free();
                }
                else
                {
                    exceptionTypes = default;
                }

                result = _cache.GetOrAdd(symbol, exceptionTypes);
            }

            return result;
        }

        private DocumentedExceptionTypesBuilder? GetDocumentedExceptionTypesFromSyntax(ISymbol symbol, [NotNullIfNotNull("symbolStack")] ref SymbolStack? symbolStack, CancellationToken cancellationToken)
        {
            DocumentedExceptionTypesBuilder? builder = null;

            foreach (var declarationReference in symbol.DeclaringSyntaxReferences)
            {
                SemanticModel? semanticModel = null;

                var declaration = declarationReference.GetSyntax(cancellationToken);

                switch (declaration.Kind())
                {
                    case SyntaxKind.VariableDeclarator:
                        if (symbol.Kind == SymbolKind.Event)
                        {
                            do
                            {
                                declaration = declaration.Parent;
                                if (declaration == null)
                                {
                                    Debug.Assert(false, $"Expected event field variable declarator to have event field declaration ancestor.");
                                    goto default;
                                }
                            }
                            while (!declaration.IsKind(SyntaxKind.EventFieldDeclaration));
                            break;
                        }
                        else if (symbol.Kind == SymbolKind.Field)
                        {
                            do
                            {
                                declaration = declaration.Parent;
                                if (declaration == null)
                                {
                                    Debug.Assert(false, $"Expected field variable declarator to have field declaration ancestor.");
                                    goto default;
                                }
                            }
                            while (!declaration.IsKind(SyntaxKind.FieldDeclaration));
                            break;
                        }
                        goto default;

                    case SyntaxKind.LocalFunctionStatement:
                    case SyntaxKind.DelegateDeclaration:
                    case SyntaxKind.MethodDeclaration:
                    case SyntaxKind.OperatorDeclaration:
                    case SyntaxKind.ConversionOperatorDeclaration:
                    case SyntaxKind.ConstructorDeclaration:
                    case SyntaxKind.DestructorDeclaration:
                    case SyntaxKind.PropertyDeclaration:
                    case SyntaxKind.EventDeclaration:
                    case SyntaxKind.IndexerDeclaration:
                        break;

                    default:
                        continue;
                }

                foreach (var trivia in declaration.GetLeadingTrivia())
                {
                    switch (trivia.Kind())
                    {
                        case SyntaxKind.SingleLineDocumentationCommentTrivia:
                        case SyntaxKind.MultiLineDocumentationCommentTrivia:
                            break;

                        default:
                            continue;
                    }

                    if (builder == null)
                        builder = DocumentedExceptionTypesBuilder.Allocate();

                    var documentationComment = (DocumentationCommentTriviaSyntax?)trivia.GetStructure();
                    if (documentationComment == null)
                        continue;

                    foreach (var xmlNode in documentationComment.Content)
                    {
                        XmlNameSyntax xmlName;
                        SyntaxList<XmlAttributeSyntax> xmlAttributes;

                        switch (xmlNode.Kind())
                        {
                            case SyntaxKind.XmlElement:
                                var xmlElement = (XmlElementSyntax)xmlNode;
                                var xmlElementStartTag = xmlElement.StartTag;
                                xmlName = xmlElementStartTag.Name;
                                xmlAttributes = xmlElementStartTag.Attributes;
                                break;
                            case SyntaxKind.XmlEmptyElement:
                                var xmlEmptyElement = (XmlEmptyElementSyntax)xmlNode;
                                xmlName = xmlEmptyElement.Name;
                                xmlAttributes = xmlEmptyElement.Attributes;
                                break;
                            default:
                                continue;
                        }

                        if (xmlName.Prefix == null &&
                            XmlEquals(xmlName.LocalName.ValueText, "exception"))
                        {
                            ISymbol? crefSymbol = null;
                            string? accessor = null;
                            foreach (var xmlAttribute in xmlAttributes)
                            {
                                if (xmlAttribute.Kind() == SyntaxKind.XmlCrefAttribute &&
                                    xmlAttribute.Name.Prefix == null &&
                                    XmlEquals(xmlAttribute.Name.LocalName.ValueText, "cref"))
                                {
                                    var xmlCrefAttribute = (XmlCrefAttributeSyntax)xmlAttribute;
                                    var cref = xmlCrefAttribute.Cref;

                                    if (semanticModel == null)
                                        semanticModel = Compilation.GetSemanticModel(declaration.SyntaxTree, ignoreAccessibility: true);

                                    crefSymbol = semanticModel.GetSymbolInfo(cref, cancellationToken).Symbol?.OriginalDefinition;
                                }
                                else if (xmlAttribute.Kind() == SyntaxKind.XmlTextAttribute &&
                                         xmlAttribute.Name.Prefix == null &&
                                         XmlEquals(xmlAttribute.Name.LocalName.ValueText, "accessor"))
                                {
                                    var xmlTextAttribute = (XmlTextAttributeSyntax)xmlAttribute;
                                    accessor = GetValueText(xmlTextAttribute.TextTokens);
                                }
                            }

                            HandleExceptionTag(symbol, builder, null, crefSymbol, accessor);
                        }
                        else if (xmlName.Prefix == null &&
                                 XmlEquals(xmlName.LocalName.ValueText, "inheritdoc"))
                        {
                            ISymbol? crefSymbol = null;
                            foreach (var xmlAttribute in xmlAttributes)
                            {
                                if (xmlAttribute.Kind() == SyntaxKind.XmlCrefAttribute &&
                                    xmlAttribute.Name.Prefix == null &&
                                    XmlEquals(xmlAttribute.Name.LocalName.ValueText, "cref"))
                                {
                                    var xmlCrefAttribute = (XmlCrefAttributeSyntax)xmlAttribute;
                                    var cref = xmlCrefAttribute.Cref;

                                    if (semanticModel == null)
                                        semanticModel = Compilation.GetSemanticModel(declaration.SyntaxTree, ignoreAccessibility: true);

                                    crefSymbol = semanticModel.GetSymbolInfo(cref, cancellationToken).Symbol;
                                }
                            }

                            HandleInheritDocTag(symbol, builder, null, crefSymbol, ref symbolStack, cancellationToken);
                        }
                    }
                }
            }

            return builder;

            static string GetValueText(SyntaxTokenList textTokens)
            {
                switch (textTokens.Count)
                {
                    case 0:
                        return string.Empty;
                    case 1:
                        return textTokens[0].ValueText;
                    default:
                        string[] strings = new string[textTokens.Count];
                        for (int i = 0; i < textTokens.Count; i++)
                            strings[i] = textTokens[i].ValueText;
                        return string.Concat(strings);
                }
            }
        }

        private DocumentedExceptionTypesBuilder? GetDocumentedExceptionTypesFromSymbol(ISymbol symbol, [NotNullIfNotNull("symbolStack")] ref SymbolStack? symbolStack, CancellationToken cancellationToken)
        {
            string? documentationCommentXml = symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken);
            if (string.IsNullOrEmpty(documentationCommentXml))
                return GetDocumentedExceptionTypesFromXmlFile(symbol, ref symbolStack, cancellationToken);

            var builder = DocumentedExceptionTypesBuilder.Allocate();

            try
            {
                using (var reader = new StringReader(documentationCommentXml))
                using (var xmlReader = XmlReader.Create(reader, _xmlReaderSettings))
                {
                    _ = xmlReader.Read();

                    // Workaround for https://github.com/dotnet/roslyn/issues/56733
                    // Metadata symbol documentation has a root "doc" element (if from
                    // VisualStudioDocumentationProvider) or is missing the root element
                    // (if from XmlDocumentationProvider).
                    // Source symbol documentation has a root "member" element.
                    while (xmlReader.NodeType != XmlNodeType.None)
                    {
                        if (xmlReader.NodeType == XmlNodeType.Element)
                        {
                            if (xmlReader.Name == "doc" ||
                                xmlReader.Name == "member")
                            {
                                _ = xmlReader.Read();
                            }
                            break;
                        }

                        xmlReader.Skip();
                    }

                    while (xmlReader.NodeType != XmlNodeType.None &&
                           xmlReader.NodeType != XmlNodeType.EndElement)
                    {
                        if (xmlReader.NodeType == XmlNodeType.Element)
                        {
                            if (XmlEquals(xmlReader.Name, "exception"))
                            {
                                string? cref = null;
                                string? accessor = null;
                                while (xmlReader.MoveToNextAttribute())
                                {
                                    if (XmlEquals(xmlReader.Name, "cref"))
                                        cref = xmlReader.Value;
                                    else if (XmlEquals(xmlReader.Name, "accessor"))
                                        accessor = xmlReader.Value;
                                }

                                HandleExceptionTag(symbol, builder, cref, null, accessor);
                            }
                            else if (XmlEquals(xmlReader.Name, "inheritdoc"))
                            {
                                string? cref = null;
                                while (xmlReader.MoveToNextAttribute())
                                {
                                    if (XmlEquals(xmlReader.Name, "cref"))
                                        cref = xmlReader.Value;
                                }

                                HandleInheritDocTag(symbol, builder, cref, null, ref symbolStack, cancellationToken);
                            }
                        }

                        xmlReader.Skip();
                    }

                    while (xmlReader.NodeType != XmlNodeType.None)
                        xmlReader.Skip();
                }
            }
            catch (XmlException)
            {
                // Nothing to do.
            }

            return builder;
        }

        private DocumentedExceptionTypesBuilder? GetDocumentedExceptionTypesFromXmlFile(ISymbol symbol, [NotNullIfNotNull("symbolStack")] ref SymbolStack? symbolStack, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? symbolId = symbol.GetDeclarationDocumentationCommentId();
            if (symbolId == null)
                return null;

            var assembly = symbol.ContainingAssembly;

            var metadataReference = Compilation.GetMetadataReference(assembly);
            if (!(metadataReference is PortableExecutableReference peReference))
                return null;

            var memberInfos = DocumentationXmlFileCache.GetMemberInfos(peReference);
            if (memberInfos == null)
                return null;

            if (!memberInfos.TryGetValue(symbolId, out var memberInfo))
                return null;

            var builder = DocumentedExceptionTypesBuilder.Allocate();

            foreach ((string cref, string? accessor) in memberInfo.Exceptions)
                HandleExceptionTag(symbol, builder, cref, null, accessor);

            foreach (string cref in memberInfo.InheritDocCrefs)
                HandleInheritDocTag(symbol, builder, cref, null, ref symbolStack, cancellationToken);

            return builder;
        }

        private void HandleExceptionTag(ISymbol symbol, DocumentedExceptionTypesBuilder builder, string? cref, ISymbol? crefSymbol, string? accessor)
        {
            _ = symbol;

            if (!DocumentedExceptionType.TryGetAccessorKind(accessor, out var accessorKind))
                return;

            if (cref != null || crefSymbol != null)
            {
                if (cref != null)
                    crefSymbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(cref, Compilation);
                if (crefSymbol is INamedTypeSymbol exceptionType)
                    builder.Add(symbol, exceptionType, accessorKind);
            }
        }

        private void HandleInheritDocTag(ISymbol symbol, DocumentedExceptionTypesBuilder builder, string? cref, ISymbol? crefSymbol, [NotNullIfNotNull("symbolStack")] ref SymbolStack? symbolStack, CancellationToken cancellationToken)
        {
            if (cref != null || crefSymbol != null)
            {
                // Prevent infinite recursion.
                if (symbolStack == null)
                    symbolStack = new SymbolStack();
                symbolStack.Push(symbol);
                try
                {
                    if (cref != null)
                        crefSymbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(cref, Compilation);
                    if (crefSymbol != null && !symbolStack.Contains(crefSymbol))
                        foreach (var documentedExceptionType in GetDocumentedExceptionTypes(crefSymbol, ref symbolStack, cancellationToken))
                            builder.Add(documentedExceptionType);
                }
                finally
                {
                    _ = symbolStack.Pop();
                }
            }
            else
            {
                switch (symbol.Kind)
                {
                    case SymbolKind.Event:
                        var eventSymbol = (IEventSymbol)symbol;
                        GetInheritedDocumentedExceptionTypes(symbol, builder, eventSymbol.OverriddenEvent, cancellationToken);
                        break;
                    case SymbolKind.Method:
                        var methodSymbol = (IMethodSymbol)symbol;
                        GetInheritedDocumentedExceptionTypes(symbol, builder, methodSymbol.OverriddenMethod, cancellationToken);
                        break;
                    case SymbolKind.Property:
                        var propertySymbol = (IPropertySymbol)symbol;
                        GetInheritedDocumentedExceptionTypes(symbol, builder, propertySymbol.OverriddenProperty, cancellationToken);
                        break;
                    default:
                        Debug.Assert(false, $"Inheriting documentation but symbol kind is {symbol.Kind}.");
                        break;
                }
            }
        }

        private void GetInheritedDocumentedExceptionTypes(ISymbol symbol, DocumentedExceptionTypesBuilder builder, ISymbol? overriddenSymbol, CancellationToken cancellationToken)
        {
            // Get from overridden symbol.
            if (overriddenSymbol != null)
                foreach (var exceptionType in GetDocumentedExceptionTypes(overriddenSymbol, cancellationToken))
                    builder.Add(exceptionType);

            // Get from implemented symbol(s).
            foreach (var interfaceSymbol in symbol.ContainingType.Interfaces)
            {
                foreach (var interfaceMemberSymbol in interfaceSymbol.GetMembers())
                {
                    if (interfaceMemberSymbol.Kind != symbol.Kind)
                        continue;

                    var implementationSymbol = symbol.ContainingType.FindImplementationForInterfaceMember(interfaceMemberSymbol);
                    if (implementationSymbol != null && SymbolEqualityComparer.Default.Equals(implementationSymbol, symbol))
                        foreach (var exceptionType in GetDocumentedExceptionTypes(interfaceMemberSymbol, cancellationToken))
                            builder.Add(exceptionType);
                }
            }
        }

        private bool TryGetExceptionAdjustments(ISymbol symbol, out AdjustmentsInfo adjustmentInfo, CancellationToken cancellationToken)
        {
            symbol = symbol.GetDeclarationSymbol();

            if (_lazyAdjustmentsCache == null)
            {
                var builder = ImmutableDictionary.CreateBuilder<ISymbol, AdjustmentsInfo>(SymbolEqualityComparer.Default);

                foreach (var entry in ExceptionAdjustments.Global)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string entrySymbolId = entry.Key;

                    foreach (var entrySymbol in DocumentationCommentId.GetSymbolsForDeclarationId(entrySymbolId, Compilation))
                        builder.Add(entrySymbol, new AdjustmentsInfo(entry.Value, hasCompiliationAdjustments: false));
                }

                foreach (var entry in _adjustments)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string entrySymbolId = entry.Key;

                    foreach (var entrySymbol in DocumentationCommentId.GetSymbolsForDeclarationId(entrySymbolId, Compilation))
                    {
                        var adjustments = entry.Value;

                        if (builder.TryGetValue(entrySymbol, out var oldValue))
                            adjustments = ExceptionAdjustments.ApplyAdjustments(oldValue.Adjustments, adjustments);

                        builder[entrySymbol] = new AdjustmentsInfo(adjustments, hasCompiliationAdjustments: true);
                    }
                }

                var adjustmentsCache = builder.ToImmutable();

                _ = Interlocked.CompareExchange(ref _lazyAdjustmentsCache, adjustmentsCache, null);
            }

            return _lazyAdjustmentsCache.TryGetValue(symbol, out adjustmentInfo);
        }

        internal readonly struct AdjustmentsInfo
        {
            public readonly ImmutableArray<MemberExceptionAdjustment> Adjustments;
            public readonly bool HasCompilationAdjustments;

            public AdjustmentsInfo(ImmutableArray<MemberExceptionAdjustment> adjustments, bool hasCompiliationAdjustments)
            {
                Adjustments = adjustments;
                HasCompilationAdjustments = hasCompiliationAdjustments;
            }
        }
    }
}
