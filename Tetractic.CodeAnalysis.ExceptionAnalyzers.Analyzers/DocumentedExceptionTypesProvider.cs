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

        private readonly ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> _adjustments;

        private readonly ConcurrentDictionary<ISymbol, ImmutableArray<DocumentedExceptionType>> _cache;

        public DocumentedExceptionTypesProvider(Compilation compilation, ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> adjustments)
        {
            Compilation = compilation;
            _adjustments = adjustments;
            _cache = new ConcurrentDictionary<ISymbol, ImmutableArray<DocumentedExceptionType>>();
        }

        public Compilation Compilation { get; }

        public ImmutableArray<DocumentedExceptionType> GetDocumentedExceptionTypes(ISymbol symbol, CancellationToken cancellationToken)
        {
            SymbolStack symbolStack = null;
            return GetDocumentedExceptionTypes(symbol, ref symbolStack, cancellationToken);
        }

        public bool TryGetDocumentedExceptionTypes(ISymbol symbol, out ImmutableArray<DocumentedExceptionType> exceptionTypes, CancellationToken cancellationToken)
        {
            SymbolStack symbolStack = null;
            return TryGetDocumentedExceptionTypes(symbol, out exceptionTypes, ref symbolStack, cancellationToken);
        }

        /// <exception cref="ArgumentOutOfRangeException"><paramref name="accessorKind"/> is not
        ///     defined.</exception>
        internal static string GetAccessorName(AccessorKind accessorKind)
        {
            switch (accessorKind)
            {
                case AccessorKind.Unspecified:
                    return null;
                case AccessorKind.Get:
                    return "get";
                case AccessorKind.Set:
                    return "set";
                case AccessorKind.Add:
                    return "add";
                case AccessorKind.Remove:
                    return "remove";
                default:
                    throw new ArgumentOutOfRangeException(nameof(accessorKind));
            }
        }

        private static bool TryGetAccessorKind(string accessor, out AccessorKind accessorKind)
        {
            switch (accessor)
            {
                case null:
                    accessorKind = AccessorKind.Unspecified;
                    return true;
                case "get":
                    accessorKind = AccessorKind.Get;
                    return true;
                case "set":
                    accessorKind = AccessorKind.Set;
                    return true;
                case "add":
                    accessorKind = AccessorKind.Add;
                    return true;
                case "remove":
                    accessorKind = AccessorKind.Remove;
                    return true;
                default:
                    accessorKind = default;
                    return false;
            }
        }

        private static void AddExceptionType(DocumentedExceptionTypesBuilder builder, ISymbol symbol, INamedTypeSymbol exceptionType, AccessorKind accessorKind)
        {
            switch (symbol.Kind)
            {
                case SymbolKind.Event:
                    if (accessorKind == AccessorKind.Unspecified)
                    {
                        var eventSymbol = (IEventSymbol)symbol;
                        if (eventSymbol.AddMethod != null)
                            builder.Add(exceptionType, AccessorKind.Add);
                        if (eventSymbol.RemoveMethod != null)
                            builder.Add(exceptionType, AccessorKind.Remove);
                        break;
                    }

                    goto default;

                case SymbolKind.Property:
                    if (accessorKind == AccessorKind.Unspecified)
                    {
                        var propertySymbol = (IPropertySymbol)symbol;
                        if (propertySymbol.GetMethod != null)
                            builder.Add(exceptionType, AccessorKind.Get);
                        if (propertySymbol.SetMethod != null)
                            builder.Add(exceptionType, AccessorKind.Set);
                        break;
                    }

                    goto default;

                default:
                    builder.Add(exceptionType, accessorKind);
                    break;
            }
        }

        private static void RemoveExceptionType(DocumentedExceptionTypesBuilder builder, ISymbol symbol, INamedTypeSymbol exceptionType, AccessorKind accessorKind)
        {
            switch (symbol.Kind)
            {
                case SymbolKind.Event:
                    if (accessorKind == AccessorKind.Unspecified)
                    {
                        var eventSymbol = (IEventSymbol)symbol;
                        if (eventSymbol.AddMethod != null)
                            _ = builder.Remove(exceptionType, AccessorKind.Add);
                        if (eventSymbol.RemoveMethod != null)
                            _ = builder.Remove(exceptionType, AccessorKind.Remove);
                        break;
                    }

                    goto default;

                case SymbolKind.Property:
                    if (accessorKind == AccessorKind.Unspecified)
                    {
                        var propertySymbol = (IPropertySymbol)symbol;
                        if (propertySymbol.GetMethod != null)
                            _ = builder.Remove(exceptionType, AccessorKind.Get);
                        if (propertySymbol.SetMethod != null)
                            _ = builder.Remove(exceptionType, AccessorKind.Set);
                        break;
                    }

                    goto default;

                default:
                    _ = builder.Remove(exceptionType, accessorKind);
                    break;
            }
        }

        private static bool XmlEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private ImmutableArray<DocumentedExceptionType> GetDocumentedExceptionTypes(ISymbol symbol, ref SymbolStack symbolStack, CancellationToken cancellationToken)
        {
            var result = GetDocumentedExceptionTypesOrDefault(symbol, ref symbolStack, cancellationToken);

            return result.IsDefault ? ImmutableArray<DocumentedExceptionType>.Empty : result;
        }

        private bool TryGetDocumentedExceptionTypes(ISymbol symbol, out ImmutableArray<DocumentedExceptionType> exceptionTypes, ref SymbolStack symbolStack, CancellationToken cancellationToken)
        {
            exceptionTypes = GetDocumentedExceptionTypesOrDefault(symbol, ref symbolStack, cancellationToken);

            return !exceptionTypes.IsDefault;
        }

        private ImmutableArray<DocumentedExceptionType> GetDocumentedExceptionTypesOrDefault(ISymbol symbol, ref SymbolStack symbolStack, CancellationToken cancellationToken)
        {
            symbol = symbol.OriginalDefinition;

            if (!_cache.TryGetValue(symbol, out var result))
            {
                var exceptionTypes = SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, Compilation.Assembly)
                    ? GetDocumentedExceptionTypesOrDefaultFromSyntax(symbol, ref symbolStack, cancellationToken)
                    : GetDocumentedExceptionTypesOrDefaultFromSymbol(symbol, ref symbolStack, cancellationToken);

                if (exceptionTypes.IsDefault)
                    exceptionTypes = GetAdjustmentAddedDocumentedExceptionTypes(symbol);

                result = _cache.GetOrAdd(symbol, exceptionTypes);
            }

            return result;
        }

        private ImmutableArray<DocumentedExceptionType> GetDocumentedExceptionTypesOrDefaultFromSyntax(ISymbol symbol, ref SymbolStack symbolStack, CancellationToken cancellationToken)
        {
            DocumentedExceptionTypesBuilder builder = null;

            foreach (var declarationReference in symbol.DeclaringSyntaxReferences)
            {
                SemanticModel semanticModel = null;

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
                            while (declaration.Kind() != SyntaxKind.EventFieldDeclaration);
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

                    var documentationComment = (DocumentationCommentTriviaSyntax)trivia.GetStructure();

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
                            ISymbol crefSymbol = null;
                            string accessor = null;
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
                            ISymbol crefSymbol = null;
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

            AdjustDocumentedExceptionTypes(symbol, ref builder);

            if (builder != null)
            {
                var exceptionTypes = builder.ToImmutable();
                builder.Free();

                return exceptionTypes;
            }

            return default;

            string GetValueText(SyntaxTokenList textTokens)
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

        private ImmutableArray<DocumentedExceptionType> GetDocumentedExceptionTypesOrDefaultFromSymbol(ISymbol symbol, ref SymbolStack symbolStack, CancellationToken cancellationToken)
        {
            string documentationCommentXml = symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken);
            if (string.IsNullOrEmpty(documentationCommentXml))
                return GetDocumentedExceptionTypesOrDefaultFromXmlFile(symbol, ref symbolStack, cancellationToken);

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
                                string cref = null;
                                string accessor = null;
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
                                string cref = null;
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

            AdjustDocumentedExceptionTypes(symbol, ref builder);

            var exceptionTypes = builder.ToImmutable();
            builder.Free();

            return exceptionTypes;
        }

        private ImmutableArray<DocumentedExceptionType> GetDocumentedExceptionTypesOrDefaultFromXmlFile(ISymbol symbol, ref SymbolStack symbolStack, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string symbolId = symbol.OriginalDefinition.GetDocumentationCommentId();
            if (symbolId == null)
                return default;

            var assembly = symbol.ContainingAssembly;

            var metadataReference = Compilation.GetMetadataReference(assembly);
            if (!(metadataReference is PortableExecutableReference peReference))
                return default;

            var memberInfos = DocumentationXmlFileCache.GetMemberInfos(peReference);
            if (memberInfos == null)
                return default;

            if (!memberInfos.TryGetValue(symbolId, out var memberInfo))
                return default;

            var builder = DocumentedExceptionTypesBuilder.Allocate();

            foreach ((string cref, string accessor) in memberInfo.Exceptions)
                HandleExceptionTag(symbol, builder, cref, null, accessor);

            foreach (string cref in memberInfo.InheritDocCrefs)
                HandleInheritDocTag(symbol, builder, cref, null, ref symbolStack, cancellationToken);

            AdjustDocumentedExceptionTypes(symbol, ref builder);

            var exceptionTypes = builder.ToImmutable();
            builder.Free();

            return exceptionTypes;
        }

        private void HandleExceptionTag(ISymbol symbol, DocumentedExceptionTypesBuilder builder, string cref, ISymbol crefSymbol, string accessor)
        {
            _ = symbol;

            if (!TryGetAccessorKind(accessor, out var accessorKind))
                return;

            if (cref != null || crefSymbol != null)
            {
                if (cref != null)
                    crefSymbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(cref, Compilation);
                if (crefSymbol is INamedTypeSymbol exceptionType)
                    AddExceptionType(builder, symbol, exceptionType, accessorKind);
            }
        }

        private void HandleInheritDocTag(ISymbol symbol, DocumentedExceptionTypesBuilder builder, string cref, ISymbol crefSymbol, ref SymbolStack symbolStack, CancellationToken cancellationToken)
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
                    if (!symbolStack.Contains(crefSymbol))
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

        private void GetInheritedDocumentedExceptionTypes(ISymbol symbol, DocumentedExceptionTypesBuilder builder, ISymbol overriddenSymbol, CancellationToken cancellationToken)
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

        private void AdjustDocumentedExceptionTypes(ISymbol symbol, ref DocumentedExceptionTypesBuilder builder)
        {
            AdjustDocumentedExceptionTypes(symbol, ref builder, ExceptionAdjustments.Global);
            AdjustDocumentedExceptionTypes(symbol, ref builder, _adjustments);
        }

        private void AdjustDocumentedExceptionTypes(ISymbol symbol, ref DocumentedExceptionTypesBuilder builder, ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> adjustments)
        {
            string symbolId = symbol.OriginalDefinition.GetDocumentationCommentId();
            if (symbolId == null)
                return;

            if (!adjustments.TryGetValue(symbolId, out var symbolAdjustments))
                return;

            if (builder == null)
                builder = DocumentedExceptionTypesBuilder.Allocate();

            foreach (var adjustment in symbolAdjustments)
            {
                if (adjustment.Kind != ExceptionAdjustmentKind.Removal)
                    continue;
                if (!TryGetAccessorKind(adjustment.Accessor, out var accessorKind))
                    continue;

                var exceptionTypeSymbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(adjustment.ExceptionTypeId, Compilation);
                if (exceptionTypeSymbol is INamedTypeSymbol exceptionType)
                    RemoveExceptionType(builder, symbol, exceptionType, accessorKind);
            }

            foreach (var adjustment in symbolAdjustments)
            {
                if (adjustment.Kind != ExceptionAdjustmentKind.Addition)
                    continue;
                if (!TryGetAccessorKind(adjustment.Accessor, out var accessorKind))
                    continue;

                var exceptionTypeSymbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(adjustment.ExceptionTypeId, Compilation);
                if (exceptionTypeSymbol is INamedTypeSymbol exceptionType)
                    AddExceptionType(builder, symbol, exceptionType, accessorKind);
            }
        }

        private ImmutableArray<DocumentedExceptionType> GetAdjustmentAddedDocumentedExceptionTypes(ISymbol symbol)
        {
            string symbolId = symbol.OriginalDefinition.GetDocumentationCommentId();
            if (symbolId == null)
                return default;

            if (!_adjustments.TryGetValue(symbolId, out var symbolAdjustments))
                return default;

            var builder = DocumentedExceptionTypesBuilder.Allocate();

            foreach (var adjustment in symbolAdjustments)
            {
                if (adjustment.Kind != ExceptionAdjustmentKind.Addition)
                    continue;
                if (!TryGetAccessorKind(adjustment.Accessor, out var accessorKind))
                    continue;

                var exceptionTypeSymbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(adjustment.ExceptionTypeId, Compilation);
                if (exceptionTypeSymbol is INamedTypeSymbol exceptionType)
                    builder.Add(exceptionType, accessorKind);
            }

            var exceptionTypes = builder.ToImmutable();
            builder.Free();

            return exceptionTypes;
        }
    }
}
