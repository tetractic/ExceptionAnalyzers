// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Tetractic.CodeAnalysis.ExceptionAnalyzers;
using Tetractic.CommandLine;

namespace GlobalExceptionAdjustmentsTool;

internal static class Program
{
    private static readonly string[] _flags = ["thrower", "transitive"];

    private static readonly string[] _eventAccessors = ["add", "remove"];

    private static readonly string[] _propertyAccessors = ["get", "set"];

    private static readonly string?[] _noAccessors = [null];

    internal static int Main(string[] args)
    {
        var rootCommand = new RootCommand(nameof(GlobalExceptionAdjustmentsTool));

        rootCommand.HelpOption = rootCommand.AddOption(
            shortName: 'h',
            longName: "help",
            description: "Shows help.");

        {
            var showCommand = rootCommand.AddSubcommand(
                name: "show",
                description: "Shows the list of exceptions thrown by members.");

            var pathParameter = showCommand.AddParameter(
                name: "PATH",
                description: "Path of directory containing documentation XML files.");

            var prefixParameter = showCommand.AddParameter(
                name: "ID",
                description: "The ID prefix of the members to show.");

            showCommand.SetInvokeHandler(() =>
            {
                Show(pathParameter.Value, prefixParameter.Value);

                return 0;
            });
        }

        rootCommand.SetInvokeHandler(() =>
        {
            string[] paths =
            [
                @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\",
                @"C:\Program Files\dotnet\sdk\NuGetFallbackFolder\netstandard.library\2.0.3\build\netstandard2.0\ref",
                @"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.17\ref\net8.0\",
                @"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.6\ref\net9.0\",
            ];

            Check(paths);

            return 0;
        });

        try
        {
            return rootCommand.Execute(args);
        }
        catch (InvalidCommandLineException ex)
        {
            Console.Error.WriteLine(ex.Message);
            CommandHelp.WriteHelpHint(ex.Command, Console.Error);
            return -1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return -1;
        }
    }

    private static void Check(string[] paths)
    {
        var frameworkMemberInfos = new Dictionary<string, DocumentationXmlFile.MemberInfo>[paths.Length];

        for (int i = 0; i < frameworkMemberInfos.Length; i++)
        {
            string path = paths[i];

            var memberInfos = new Dictionary<string, DocumentationXmlFile.MemberInfo>();

            foreach (string xmlPath in Directory.EnumerateFiles(path, "*.xml"))
            {
                using (var stream = File.Open(xmlPath, FileMode.Open, FileAccess.Read))
                {
                    var xmlMemberInfos = DocumentationXmlFile.LoadMemberInfos(stream);
                    if (xmlMemberInfos is null)
                        continue;

                    foreach (var entry in xmlMemberInfos)
                        memberInfos[entry.Key] = entry.Value;
                }

                Debug.WriteLine(xmlPath);
            }

            frameworkMemberInfos[i] = memberInfos;
        }

        SourceText text;

        ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> memberAdjustments;

        using (var stream = typeof(ExceptionAdjustments).Assembly.GetManifestResourceStream("GlobalExceptionAdjustmentsTool.GlobalExceptionAdjustments.txt"))
        {
            text = SourceText.From(stream!);

            var adjustmentsFile = ExceptionAdjustmentsFile.Load(text);

            Debug.Assert(adjustmentsFile.Diagnostics.IsEmpty);

            memberAdjustments = adjustmentsFile.MemberAdjustments;
        }

        foreach (var entry in memberAdjustments.OrderBy(x => x.Key))
        {
            string memberId = entry.Key;
            var adjustments = entry.Value;

            bool exists = false;
            var used = new BitArray(adjustments.Length);

            for (int i = 0; i < frameworkMemberInfos.Length; ++i)
            {
                if (!frameworkMemberInfos[i].TryGetValue(memberId, out var memberInfo))
                    continue;

                Debug.Assert(memberInfo.InheritDocCrefs.IsEmpty);

                exists |= true;

                foreach (string? accessor in GetAccessors(memberId))
                {
                    var exceptionIds = memberInfo.Exceptions
                        .Where(x => x.Accessor == accessor || x.Accessor is null)
                        .Select(x => x.Cref)
                        .ToHashSet();

                    ApplyAdjustments(exceptionIds, adjustments, used, accessor: null);

                    if (accessor is not null)
                        ApplyAdjustments(exceptionIds, adjustments, used, accessor);

                    foreach (string flag in _flags)
                    {
                        bool? result = null;

                        result = ApplyFlagAdjustments(result, exceptionIds, adjustments, used, flag, accessor: null);

                        if (accessor is not null)
                            result = ApplyFlagAdjustments(result, exceptionIds, adjustments, used, flag, accessor);
                    }
                }
            }

            if (!exists)
            {
                Console.Error.WriteLine($"Member does not exist: {memberId}");
            }
            else
            {
                for (int i = 0; i < adjustments.Length; ++i)
                {
                    if (!used[i])
                    {
                        var adjustment = adjustments[i];

                        int line = text.Lines.GetLinePosition(adjustment.SymbolIdSpan.Start).Line + 1;
                        var span = TextSpan.FromBounds(adjustment.SymbolIdSpan.Start, adjustment.ExceptionTypeIdSpan.End);
                        Console.Error.WriteLine($"Line {line} is unused: {text.ToString(span)}");
                    }
                }
            }
        }
    }

    private static void Show(string path, string prefix)
    {
        var memberInfos = new Dictionary<string, DocumentationXmlFile.MemberInfo>();

        foreach (string xmlPath in Directory.EnumerateFiles(path, "*.xml"))
        {
            using (var stream = File.Open(xmlPath, FileMode.Open, FileAccess.Read))
            {
                var xmlMemberInfos = DocumentationXmlFile.LoadMemberInfos(stream);
                if (xmlMemberInfos is null)
                    continue;

                foreach (var entry in xmlMemberInfos)
                    memberInfos[entry.Key] = entry.Value;
            }

            Debug.WriteLine(xmlPath);
        }

        SourceText text;

        ImmutableDictionary<string, ImmutableArray<MemberExceptionAdjustment>> memberAdjustments;

        using (var stream = typeof(ExceptionAdjustments).Assembly.GetManifestResourceStream("GlobalExceptionAdjustmentsTool.GlobalExceptionAdjustments.txt"))
        {
            text = SourceText.From(stream!);

            var adjustmentsFile = ExceptionAdjustmentsFile.Load(text);

            Debug.Assert(adjustmentsFile.Diagnostics.IsEmpty);

            memberAdjustments = adjustmentsFile.MemberAdjustments;
        }

        foreach (var entry in memberInfos.OrderBy(x => x.Key))
        {
            string memberId = entry.Key;
            var memberInfo = entry.Value;

            if (!memberId.StartsWith(prefix))
                continue;

            foreach (string? accessor in GetAccessors(memberId))
            {
                var exceptionIds = memberInfo.Exceptions
                    .Where(x => x.Accessor == accessor || x.Accessor is null)
                    .Select(x => x.Cref)
                    .ToHashSet();

                if (memberAdjustments.TryGetValue(memberId, out var adjustments))
                {
                    ApplyAdjustments(exceptionIds, adjustments, used: null, accessor: null);

                    if (accessor is not null)
                        ApplyAdjustments(exceptionIds, adjustments, used: null, accessor);
                }

                if (accessor is not null)
                    Console.WriteLine($"{memberId} {accessor}");
                else
                    Console.WriteLine($"{memberId}");

                foreach (string exceptionId in exceptionIds.OrderBy(x => x))
                    Console.WriteLine($"\t{exceptionId}");

                Console.WriteLine();
            }
        }
    }

    private static IEnumerable<string?> GetAccessors(string memberId)
    {
        if (memberId.StartsWith("E:", StringComparison.Ordinal))
            return _eventAccessors;
        else if (memberId.StartsWith("M:", StringComparison.Ordinal))
            return _noAccessors;
        else if (memberId.StartsWith("P:", StringComparison.Ordinal))
            return _propertyAccessors;
        else
            throw new ArgumentException("Unexpected member type.", nameof(memberId));
    }

    private static void ApplyAdjustments(HashSet<string> exceptionIds, ImmutableArray<MemberExceptionAdjustment> adjustments, BitArray? used, string? accessor)
    {
        for (int i = 0; i < adjustments.Length; ++i)
        {
            var adjustment = adjustments[i];

            if (adjustment.Flag is not null)
                continue;
            if (adjustment.Accessor != accessor)
                continue;
            if (adjustment.Kind != ExceptionAdjustmentKind.Removal)
                continue;

            bool removed = exceptionIds.Remove(adjustment.ExceptionTypeId);
            if (used is not null)
                used[i] |= removed;
        }

        for (int i = 0; i < adjustments.Length; ++i)
        {
            var adjustment = adjustments[i];

            if (adjustment.Flag is not null)
                continue;
            if (adjustment.Accessor != accessor)
                continue;
            if (adjustment.Kind != ExceptionAdjustmentKind.Addition)
                continue;

            bool added = exceptionIds.Add(adjustment.ExceptionTypeId);
            if (used is not null)
                used[i] |= added;
        }
    }

    private static bool? ApplyFlagAdjustments(bool? result, HashSet<string> exceptionIds, ImmutableArray<MemberExceptionAdjustment> adjustments, BitArray used, string flag, string? accessor)
    {
        for (int i = 0; i < adjustments.Length; ++i)
        {
            var adjustment = adjustments[i];

            if (adjustment.Flag != flag)
                continue;
            if (adjustment.Accessor != accessor)
                continue;
            if (adjustment.Kind != ExceptionAdjustmentKind.Removal)
                continue;

            used[i] |= result != false && exceptionIds.Contains(adjustment.ExceptionTypeId);
            result = false;
        }

        for (int i = 0; i < adjustments.Length; ++i)
        {
            var adjustment = adjustments[i];

            if (adjustment.Flag != flag)
                continue;
            if (adjustment.Accessor != accessor)
                continue;
            if (adjustment.Kind != ExceptionAdjustmentKind.Addition)
                continue;

            used[i] |= result != true && exceptionIds.Contains(adjustment.ExceptionTypeId);
            result = true;
        }

        return result;
    }
}
