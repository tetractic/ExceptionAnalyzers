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

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal sealed partial class DocumentedExceptionTypesProvider
    {
        private static class DocumentationXmlFileCache
        {
            private static readonly ConditionalWeakTable<MetadataReference, ImmutableDictionary<string, DocumentationXmlFile.MemberInfo>?> _cache = new ConditionalWeakTable<MetadataReference, ImmutableDictionary<string, DocumentationXmlFile.MemberInfo>?>();

            public static ImmutableDictionary<string, DocumentationXmlFile.MemberInfo>? GetMemberInfos(PortableExecutableReference peReference)
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

                        // If the path looks like a path to a reference assembly of a referenced
                        // project then we may find the XML file in the parent directory instead.
                        string? directoryPath = Path.GetDirectoryName(xmlPath);
                        string? directoryName = Path.GetFileName(directoryPath);
                        if ("ref".Equals(directoryName, StringComparison.OrdinalIgnoreCase) && !File.Exists(xmlPath))
                        {
                            directoryPath = Path.GetDirectoryName(directoryPath);
                            if (directoryPath != null)
                            {
                                string xmlFileName = Path.GetFileName(xmlPath);
                                xmlPath = Path.Combine(directoryPath, xmlFileName);

                                Debug.WriteLine($"Loading documentation XML: {xmlPath}");
                            }
                        }

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
                            memberInfos = DocumentationXmlFile.LoadMemberInfos(stream);
                    }
                    finally
                    {
                        memberInfos = _cache.GetValue(peReference, _ => memberInfos);
                    }
                }

                return memberInfos;
            }
        }
    }
}
