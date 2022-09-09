// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal sealed partial class DocumentedExceptionTypesProvider
    {
        private static class DocumentationXmlFileCache
        {
            private static readonly ConditionalWeakTable<MetadataReference, ImmutableDictionary<string, MemberInfo>?> _cache = new ConditionalWeakTable<MetadataReference, ImmutableDictionary<string, MemberInfo>?>();

            public static ImmutableDictionary<string, MemberInfo>? GetMemberInfos(PortableExecutableReference peReference)
            {
                string? pePath = peReference.FilePath;
                if (string.IsNullOrEmpty(pePath))
                    return null;

                if (!_cache.TryGetValue(peReference, out var memberInfos))
                {
                    try
                    {
                        string xmlPath;
                        try
                        {
                            xmlPath = Path.ChangeExtension(pePath, "xml");
                        }
                        catch (ArgumentException)
                        {
                            return null;
                        }

                        Debug.WriteLine($"Loading documentation XML: {xmlPath}");

                        Stream stream;
                        try
                        {
#pragma warning disable CA2000 // Dispose objects before losing scope
                            stream = new FileStream(xmlPath, FileMode.Open, FileAccess.Read);
#pragma warning restore CA2000 // Dispose objects before losing scope
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch
#pragma warning restore CA1031 // Do not catch general exception types
                        {
                            return null;
                        }

                        using (stream)
                            memberInfos = LoadMemberInfos(stream);
                    }
                    finally
                    {
                        memberInfos = _cache.GetValue(peReference, _ => memberInfos);
                    }
                }

                return memberInfos;
            }

            private static ImmutableDictionary<string, MemberInfo>? LoadMemberInfos(Stream stream)
            {
                try
                {
                    using (var xmlReader = XmlReader.Create(stream))
                    {
                        if (!xmlReader.Read())
                            return null;

                        // Workaround for https://github.com/dotnet/standard/issues/1527
                        // netstandard.xml has a root "span" element and mismatched tags.
                        while (xmlReader.NodeType != XmlNodeType.None)
                        {
                            if (xmlReader.NodeType == XmlNodeType.Element)
                            {
                                if (xmlReader.Name == "span")
                                {
                                    xmlReader.Dispose();

                                    stream.Position = 0;
                                    string xml;
                                    using (var reader = new StreamReader(stream))
                                        xml = reader.ReadToEnd();

                                    xml = Regex.Replace(xml, "</?(br|p|span)( [^>]*)?>", "", RegexOptions.Compiled | RegexOptions.CultureInvariant);

                                    using (var fixedStream = new MemoryStream())
                                    using (var writer = new StreamWriter(fixedStream))
                                    {
                                        writer.Write(xml);
                                        writer.Flush();

                                        fixedStream.Position = 0;
                                        return LoadMemberInfos(fixedStream);
                                    }
                                }
                                break;
                            }

                            xmlReader.Skip();
                        }

                        var memberInfosBuilder = ImmutableDictionary.CreateBuilder<string, MemberInfo>();
                        var exceptionsBuilder = ImmutableArray.CreateBuilder<(string Cref, string? Accessor)>();
                        var inheritDocCrefsBuilder = ImmutableArray.CreateBuilder<string>();

                        while (xmlReader.NodeType != XmlNodeType.None)
                        {
                            if (xmlReader.NodeType == XmlNodeType.Element)
                            {
                                if (xmlReader.Name != "doc")
                                    return null;

                                if (!xmlReader.IsEmptyElement)
                                {
                                    _ = xmlReader.Read();

                                    while (xmlReader.NodeType != XmlNodeType.EndElement)
                                    {
                                        if (xmlReader.NodeType == XmlNodeType.Element &&
                                            xmlReader.Name == "members" &&
                                            !xmlReader.IsEmptyElement)
                                        {
                                            _ = xmlReader.Read();

                                            while (xmlReader.NodeType != XmlNodeType.EndElement)
                                            {
                                                if (xmlReader.NodeType == XmlNodeType.Element &&
                                                    xmlReader.Name == "member")
                                                {
                                                    string name = xmlReader.GetAttribute("name");
                                                    if (name != null)
                                                    {
                                                        if (!xmlReader.IsEmptyElement)
                                                        {
                                                            _ = xmlReader.Read();

                                                            while (xmlReader.NodeType != XmlNodeType.EndElement)
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

                                                                        if (cref != null)
                                                                            exceptionsBuilder.Add((cref, accessor));
                                                                    }
                                                                    else if (XmlEquals(xmlReader.Name, "inheritdoc"))
                                                                    {
                                                                        string? cref = null;
                                                                        while (xmlReader.MoveToNextAttribute())
                                                                        {
                                                                            if (XmlEquals(xmlReader.Name, "cref"))
                                                                                cref = xmlReader.Value;
                                                                        }

                                                                        if (cref != null)
                                                                            inheritDocCrefsBuilder.Add(cref);
                                                                    }
                                                                }

                                                                xmlReader.Skip();
                                                            }
                                                        }

                                                        memberInfosBuilder.Add(name, new MemberInfo(
                                                            exceptions: exceptionsBuilder.ToImmutable(),
                                                            inheritDocCrefs: inheritDocCrefsBuilder.ToImmutable()));

                                                        exceptionsBuilder.Clear();
                                                        inheritDocCrefsBuilder.Clear();
                                                    }
                                                }

                                                xmlReader.Skip();
                                            }
                                        }

                                        xmlReader.Skip();
                                    }
                                }
                            }

                            xmlReader.Skip();
                        }

                        return memberInfosBuilder.ToImmutable();
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    // Nothing to do.

                    return null;
                }
            }

            public readonly struct MemberInfo
            {
                public MemberInfo(ImmutableArray<(string Cref, string? Accessor)> exceptions, ImmutableArray<string> inheritDocCrefs)
                {
                    Exceptions = exceptions;
                    InheritDocCrefs = inheritDocCrefs;
                }

                public ImmutableArray<(string Cref, string? Accessor)> Exceptions { get; }

                public ImmutableArray<string> InheritDocCrefs { get; }
            }
        }
    }
}
