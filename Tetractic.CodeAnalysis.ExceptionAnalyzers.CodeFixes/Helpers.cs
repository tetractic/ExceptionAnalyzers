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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    internal static class Helpers
    {
        private const string _exceptionAdjustmentPrefix = "// ExceptionAdjustment: ";

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
            var additionalFileActions = GetAdditionAdjustmentAdditionalFileActions(document, memberId, member, accessor, exceptionTypeIdsAndTypes);

            return CodeAction.Create(
                title: "Adjust documented exceptions",
                nestedActions: additionalFileActions,
                isInlinable: false);
        }

        public static CodeAction GetRemovalAdjustmentCodeAction(Document document, string memberId, ISymbol? member, string? accessor, (string ExceptionTypeId, ISymbol ExceptionType)[] exceptionTypeIdsAndTypes)
        {
            var additionalFileActions = GetRemovalAdjustmentAdditionalFileActions(document, memberId, member, accessor, exceptionTypeIdsAndTypes);

            return CodeAction.Create(
                title: "Adjust documented exceptions",
                nestedActions: additionalFileActions,
                isInlinable: false);
        }

        public static CodeAction GetRemovalAdjustmentCodeAction(Document document, SyntaxNode declaration, string memberId, ISymbol? member, string? accessor, (string ExceptionTypeId, ISymbol ExceptionType)[] exceptionTypeIdsAndTypes)
        {
            var additionalFileActions = GetRemovalAdjustmentAdditionalFileActions(document, memberId, member, accessor, exceptionTypeIdsAndTypes);

            var codeActions = GetRemovalAdjustmentCodeActions(document, declaration, memberId, member, accessor, exceptionTypeIdsAndTypes);

            return CodeAction.Create(
                title: "Adjust documented exceptions",
                nestedActions: ImmutableArray.Create(new[]
                {
                    CodeAction.Create(
                        title: "Adjust in adjustments file",
                        nestedActions: additionalFileActions,
                        isInlinable: false),
                    CodeAction.Create(
                        title: "Adjust in code",
                        nestedActions: codeActions,
                        isInlinable: false),
                }),
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
            return accessor != null
                ? $"{memberId} {accessor} -{exceptionTypeId}"
                : $"{memberId} -{exceptionTypeId}";
        }

        private static string GetAdditionAdjustmentLine(string memberId, string? accessor, string exceptionTypeId)
        {
            return accessor != null
                ? $"{memberId} {accessor} +{exceptionTypeId}"
                : $"{memberId} +{exceptionTypeId}";
        }

        private static ImmutableArray<CodeAction> GetAdditionAdjustmentAdditionalFileActions(Document document, string memberId, ISymbol? member, string? accessor, (string ExceptionTypeId, ISymbol ExceptionType)[] exceptionTypeIdsAndTypes)
        {
            return exceptionTypeIdsAndTypes
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
        }

        private static ImmutableArray<CodeAction> GetRemovalAdjustmentAdditionalFileActions(Document document, string memberId, ISymbol? member, string? accessor, (string ExceptionTypeId, ISymbol ExceptionType)[] exceptionTypeIdsAndTypes)
        {
            return exceptionTypeIdsAndTypes
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
            int position = text.Length;
            string? symbolId;
            MemberExceptionAdjustment adjustment;
            if (TryParseAdjustment(line, out symbolId, out adjustment))
            {
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
                    if (ShouldInsertAfter(existingLine, symbolId, adjustment))
                        break;
                    position = textLine.Span.Start;
                }
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

            static bool ShouldInsertAfter(string existingLine, string symbolId, MemberExceptionAdjustment adjustment)
            {
                if (existingLine.Length < 1 || existingLine[0] == '#')
                    return true;

                if (!TryParseAdjustment(existingLine, out string? existingSymbolId, out MemberExceptionAdjustment existingAdjustment))
                    return true;

                // Compare symbol identifier (excluding kind prefix).
                int end = Math.Min(existingSymbolId.Length, symbolId.Length);
                int result = CompareSubstring(existingSymbolId, symbolId, 2, end);
                if (result != 0)
                    return result < 0;
                result = existingSymbolId.Length.CompareTo(symbolId.Length);
                if (result != 0)
                    return result < 0;

                // Compare presence of accessor.  Absence comes before presence.
                bool existingHasAccessor = existingAdjustment.Accessor != null;
                bool hasAccessor = adjustment.Accessor != null;
                if (existingHasAccessor != hasAccessor)
                    return hasAccessor;

                // Compare operator.  '-' comes before '+'.
                result = existingAdjustment.Kind.CompareTo(adjustment.Kind);
                if (result != 0)
                    return result < 0;

                // Compare accessor, if present.
                if (existingHasAccessor)
                {
                    result = string.CompareOrdinal(existingAdjustment.Accessor, adjustment.Accessor);
                    if (result != 0)
                        return result < 0;
                }

                // Compare exception type identifier.
                return string.CompareOrdinal(existingAdjustment.ExceptionTypeId, adjustment.ExceptionTypeId) < 0;
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

        private static ImmutableArray<CodeAction> GetRemovalAdjustmentCodeActions(Document document, SyntaxNode declaration, string memberId, ISymbol? member, string? accessor, (string ExceptionTypeId, ISymbol ExceptionType)[] exceptionTypeIdsAndTypes)
        {
            return exceptionTypeIdsAndTypes
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
                        createChangedDocument: async cancellationToken =>
                        {
                            // Try to remove an addition adjustment line.
                            {
                                string additionCommentLine = _exceptionAdjustmentPrefix + GetAdditionAdjustmentLine(memberId, accessor, exceptionTypeId);

                                var newDocument = TryRemoveAdjustmentCommentLines(document, declaration, additionCommentLine, cancellationToken);
                                if (newDocument != document)
                                    return newDocument;
                            }

                            // Add a removal adjustment line.
                            {
                                string removalCommentLine = _exceptionAdjustmentPrefix + GetRemovalAdjustmentLine(memberId, accessor, exceptionTypeId);

                                return TryAddAdjustmentCommentLine(document, declaration, removalCommentLine, cancellationToken);
                            }
                        });
                })
                .ToImmutableArray();
        }

        private static Document TryRemoveAdjustmentCommentLines(Document document, SyntaxNode declaration, string commentLine, CancellationToken cancellationToken)
        {
            var syntaxRoot = declaration.SyntaxTree.GetRoot(cancellationToken);

            var leadingTrivia = declaration.GetLeadingTrivia();

            var newLeadingTrivia = leadingTrivia;

            for (int i = 0; i < newLeadingTrivia.Count; ++i)
            {
                var trivia = newLeadingTrivia[i];

                if (trivia.Kind() == SyntaxKind.SingleLineCommentTrivia &&
                    trivia.ToString() == commentLine)
                {
                    newLeadingTrivia = newLeadingTrivia.RemoveAt(i);

                    if (i < newLeadingTrivia.Count &&
                        newLeadingTrivia[i].Kind() == SyntaxKind.EndOfLineTrivia)
                    {
                        newLeadingTrivia = newLeadingTrivia.RemoveAt(i);

                        if (i < newLeadingTrivia.Count &&
                            newLeadingTrivia[i].Kind() == SyntaxKind.WhitespaceTrivia)
                        {
                            newLeadingTrivia = newLeadingTrivia.RemoveAt(i);
                        }
                    }
                }
            }

            if (newLeadingTrivia == leadingTrivia)
                return document;

            var newDeclaration = declaration
                .WithLeadingTrivia(newLeadingTrivia);

            syntaxRoot = syntaxRoot.ReplaceNode(declaration, newDeclaration);

            return document.WithSyntaxRoot(syntaxRoot);
        }

        private static Document TryAddAdjustmentCommentLine(Document document, SyntaxNode declaration, string commentLine, CancellationToken cancellationToken)
        {
            var syntaxRoot = declaration.SyntaxTree.GetRoot(cancellationToken);

            var newDeclaration = declaration
                .WithLeadingTrivia(declaration.GetLeadingTrivia()
                    .Add(SyntaxFactory.Comment(commentLine))
                    .Add(SyntaxFactory.ElasticEndOfLine("\n")));

            syntaxRoot = syntaxRoot.ReplaceNode(declaration, newDeclaration);

            return document.WithSyntaxRoot(syntaxRoot);
        }

        private static bool TryParseAdjustment(string line, [NotNullWhen(true)] out string? symbolId, out MemberExceptionAdjustment adjustment)
        {
            return ExceptionAdjustmentsFile.TryParseAdjustment(line, TextSpan.FromBounds(0, line.Length), ReportDiagnostic, out symbolId, out adjustment);

            static void ReportDiagnostic(DiagnosticDescriptor descriptor, TextSpan span)
            {
                // Ignore.
            }
        }
    }
}
