// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal static class Helpers
    {
        private static readonly SymbolDisplayFormat _memberDisplayFormat = new SymbolDisplayFormat(
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        public static SyntaxTriviaList AddExceptionXmlElement(SyntaxTriviaList leadingTrivia, string cref, string? accessor)
        {
            SyntaxList<XmlAttributeSyntax> attributes;

            var crefAttribute = SyntaxFactory.XmlTextAttribute("cref", cref);

            if (accessor != null)
            {
                var accessorAttribute = SyntaxFactory.XmlTextAttribute("accessor", accessor);

                attributes = SyntaxFactory.List<XmlAttributeSyntax>(new[]
                {
                    crefAttribute,
                    accessorAttribute,
                });
            }
            else
            {
                attributes = SyntaxFactory.SingletonList<XmlAttributeSyntax>(crefAttribute);
            }

            var element = SyntaxFactory.XmlElement(
                SyntaxFactory.XmlElementStartTag(
                    SyntaxFactory.XmlName("exception"),
                    attributes),
                SyntaxFactory.XmlElementEndTag(
                    SyntaxFactory.XmlName("exception")));

            var trivia = SyntaxFactory.Trivia(
                SyntaxFactory.DocumentationComment(
                    element.WithLeadingTrivia(SyntaxTriviaList.Create(
                        SyntaxFactory.DocumentationCommentExterior("///"))),
                    SyntaxFactory.XmlText(
                        SyntaxFactory.XmlTextLiteral("\n"))));

            return leadingTrivia.Add(trivia);
        }

        public static CodeAction GetAdditionAdjustmentCodeAction(Document document, string memberId, ISymbol? member, string? accessor, (string ExceptionTypeId, ISymbol ExceptionType)[] exceptionTypeIdsAndTypes)
        {
            var codeActions = exceptionTypeIdsAndTypes
                .Select(x =>
                {
                    string exceptionTypeId = x.ExceptionTypeId;
                    var exceptionType = x.ExceptionType;

                    string exceptionName = exceptionType != null
                        ? exceptionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                        : exceptionTypeId;

                    string memberName = member != null
                        ? member.ToDisplayString(_memberDisplayFormat)
                        : memberId;

                    return CodeAction.Create(
                        title: accessor != null
                            ? $"Add '{exceptionName}' to '{memberName}' '{accessor}' accessor"
                            : $"Add '{exceptionName}' to '{memberName}'",
                        createChangedSolution: async cancellationToken =>
                        {
                            // Try to remove an removal adjustment line.
                            {
                                string removalLine = GetRemovalAdjustmentLine(memberId, accessor, exceptionTypeId);

                                var project = await TryRemoveAdjustmentLines(document.Project, removalLine, cancellationToken).ConfigureAwait(false);
                                if (project != document.Project)
                                    return project.Solution;
                            }

                            // Add a removal adjustment line.
                            {
                                string additionLine = GetAdditionAdjustmentLine(memberId, accessor, exceptionTypeId);

                                var project = await TryAddAdjustmentLine(document.Project, additionLine, cancellationToken).ConfigureAwait(false);
                                if (project != document.Project)
                                    return project.Solution;

                                string endOfLine = Environment.NewLine;

                                var text = SourceText.From(
                                    GetAdjustmentFileHeader(endOfLine) + endOfLine +
                                    @"" + endOfLine +
                                    additionLine + endOfLine);

                                return document.Project.AddAdditionalDocument(ExceptionAdjustments.FileName, text).Project.Solution;
                            }
                        });
                })
                .ToImmutableArray();

            return CodeAction.Create(
                title: "Adjust documented exceptions",
                nestedActions: codeActions,
                isInlinable: false);
        }

        public static CodeAction GetRemovalAdjustmentCodeAction(Document document, string memberId, ISymbol? member, string? accessor, (string ExceptionTypeId, ISymbol ExceptionType)[] exceptionTypeIdsAndTypes)
        {
            var codeActions = exceptionTypeIdsAndTypes
                .Select(x =>
                {
                    string exceptionTypeId = x.ExceptionTypeId;
                    var exceptionType = x.ExceptionType;

                    string exceptionName = exceptionType != null
                        ? exceptionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                        : exceptionTypeId;

                    string memberName = member != null
                        ? member.ToDisplayString(_memberDisplayFormat)
                        : memberId;

                    return CodeAction.Create(
                        title: accessor != null
                            ? $"Remove '{exceptionName}' from '{memberName}' '{accessor}' accessor"
                            : $"Remove '{exceptionName}' from '{memberName}'",
                        createChangedSolution: async cancellationToken =>
                        {
                            // Try to remove an addition adjustment line.
                            {
                                string additionLine = GetAdditionAdjustmentLine(memberId, accessor, exceptionTypeId);

                                var project = await TryRemoveAdjustmentLines(document.Project, additionLine, cancellationToken).ConfigureAwait(false);
                                if (project != document.Project)
                                    return project.Solution;
                            }

                            // Add a removal adjustment line.
                            {
                                string removalLine = GetRemovalAdjustmentLine(memberId, accessor, exceptionTypeId);

                                var project = await TryAddAdjustmentLine(document.Project, removalLine, cancellationToken).ConfigureAwait(false);
                                if (project != document.Project)
                                    return project.Solution;

                                string endOfLine = Environment.NewLine;

                                var text = SourceText.From(
                                    GetAdjustmentFileHeader(endOfLine) + endOfLine +
                                    @"" + endOfLine +
                                    removalLine + endOfLine);

                                return document.Project.AddAdditionalDocument(ExceptionAdjustments.FileName, text).Project.Solution;
                            }
                        });
                })
                .ToImmutableArray();

            return CodeAction.Create(
                title: "Adjust documented exceptions",
                nestedActions: codeActions,
                isInlinable: false);
        }

        private static string GetAdjustmentFileHeader(string endOfLine)
        {
            return @"# Due to [1], you may have to manually change the ""Build Action"" of this file to ""C# analyzer additional file""." + endOfLine +
                   @"# [1] https://github.com/dotnet/roslyn/issues/4655" + endOfLine +
                   @"" + endOfLine +
                   @"# This file adjusts exception information used by Tetractic.CodeAnalysis.ExceptionAnalyzers." + endOfLine +
                   @"# Usage: <memberId> {-|+}[<accessor>] <exceptionTypeId>" + endOfLine +
                   @"# See ECMA-334, 5th Ed. § D.4.2 ""ID string format"" for a description of the ID format.";
        }

        private static string GetEndOfLine(SourceText text)
        {
            string endOfLine = Environment.NewLine;

            if (text.Lines.Count > 0)
            {
                var firstLine = text.Lines[0];
                var endOfLineSpan = TextSpan.FromBounds(firstLine.End, firstLine.EndIncludingLineBreak);
                if (!endOfLineSpan.IsEmpty)
                    endOfLine = text.ToString(endOfLineSpan);
            }

            return endOfLine;
        }

        private static string GetRemovalAdjustmentLine(string memberId, string? accessor, string exceptionTypeId)
        {
            return $"{memberId} -{accessor} {exceptionTypeId}";
        }

        private static string GetAdditionAdjustmentLine(string memberId, string? accessor, string exceptionTypeId)
        {
            return $"{memberId} +{accessor} {exceptionTypeId}";
        }

        private static async Task<Project> TryRemoveAdjustmentLines(Project project, string line, CancellationToken cancellationToken)
        {
            var solution = project.Solution;

            foreach (var additionalDocument in project.AdditionalDocuments)
            {
                if (Path.GetFileName(additionalDocument.FilePath) == ExceptionAdjustments.FileName)
                {
                    var text = await additionalDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    if (text == null)
                        continue;

                    var textChanges = ImmutableArray<TextChange>.Empty;

                    for (int i = text.Lines.Count; i > 0; --i)
                    {
                        var textLine = text.Lines[i - 1];

                        if (textLine.ToString() == line)
                            textChanges = textChanges.Add(new TextChange(textLine.SpanIncludingLineBreak, string.Empty));
                    }

                    if (!textChanges.IsEmpty)
                        solution = solution.WithAdditionalDocumentText(additionalDocument.Id, text.WithChanges(textChanges));
                }
            }

            return solution == project.Solution
                ? project
                : solution.GetProject(project.Id)!;
        }

        private static async Task<Project> TryAddAdjustmentLine(Project project, string line, CancellationToken cancellationToken)
        {
            var adjustmentsDocument = project.AdditionalDocuments
                .OrderBy(additionalDocument => additionalDocument.Folders.Count)
                .FirstOrDefault(additionalDocument => Path.GetFileName(additionalDocument.FilePath) == ExceptionAdjustments.FileName);

            if (adjustmentsDocument != null)
            {
                var text = await adjustmentsDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                if (text != null)
                {
                    string endOfLine = GetEndOfLine(text);

                    text = AddAdjustmentLine(text, line, endOfLine);

                    return project.Solution.WithAdditionalDocumentText(adjustmentsDocument.Id, text).GetProject(project.Id)!;
                }
            }

            return project;
        }

        private static SourceText AddAdjustmentLine(SourceText text, string line, string endOfLine)
        {
            int symbolEnd = line.Length < 2 ? -1 : line.IndexOf(' ', 2);

            int position = text.Length;

            bool allEmpty = true;
            for (int i = text.Lines.Count; i > 0; --i)
            {
                var textLine = text.Lines[i - 1];
                if (textLine.Span.IsEmpty)
                {
                    if (allEmpty)
                    {
                        position = textLine.Span.Start;
                        continue;
                    }
                    break;
                }
                else
                {
                    allEmpty = false;
                }
                string existingLine = textLine.ToString();
                if (ShouldInsertAfter(existingLine, line, symbolEnd))
                    break;
                position = textLine.Span.Start;
            }

            // If inserting after last line, ensure it ends with a line break.
            if (position == text.Length && text.Lines.Count > 0)
            {
                var lastLine = text.Lines[text.Lines.Count - 1];
                if (lastLine.EndIncludingLineBreak == lastLine.End &&
                    lastLine.End != lastLine.Start)
                {
                    text = text.Replace(new TextSpan(position, 0), endOfLine);
                    position = text.Length;
                }
            }

            return text.Replace(new TextSpan(position, 0), line + endOfLine);

            static bool ShouldInsertAfter(string existingLine, string line, int symbolEnd)
            {
                if (symbolEnd < 0)
                    return true;

                if (existingLine.Length < 2 || existingLine[0] == '#' || existingLine[1] != ':')
                    return true;

                // Compare symbol identifier (excluding kind prefix).
                int end = Math.Min(existingLine.Length, symbolEnd);
                int result = CompareSubstring(existingLine, line, 2, end);
                if (result != 0)
                    return result < 0;

                // Compare presence of accessor.  Absence comes before presence.
                if (symbolEnd >= existingLine.Length - 2 || symbolEnd >= line.Length - 2)
                    return true;
                bool existingLineHasAccessor = existingLine[symbolEnd + 2] != ' ';
                bool lineHasAccessor = line[symbolEnd + 2] != ' ';
                if (existingLineHasAccessor != lineHasAccessor)
                    return lineHasAccessor;

                // Compare operator.  '-' comes before '+'.
                result = existingLine[symbolEnd + 1] - line[symbolEnd + 1];
                if (result != 0)
                    return result > 0;

                // Compare the accessor and exception type identifier.
                end = Math.Min(existingLine.Length, line.Length);
                result = CompareSubstring(existingLine, line, symbolEnd + 2, end);
                if (result != 0)
                    return result < 0;
                return existingLine.Length < line.Length;
            }

            static int CompareSubstring(string left, string right, int start, int end)
            {
                for (int i = start; i < end; ++i)
                {
                    char cLeft = left[i];
                    char cRight = right[i];
                    if (cLeft != cRight)
                        return cLeft - cRight;
                }
                return 0;
            }
        }
    }
}
