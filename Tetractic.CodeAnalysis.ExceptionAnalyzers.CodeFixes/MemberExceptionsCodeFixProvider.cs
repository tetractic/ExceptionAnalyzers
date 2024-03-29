﻿// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MemberExceptionsCodeFixProvider)), Shared]
    public sealed class MemberExceptionsCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(new[]
        {
            MemberExceptionsAnalyzer.DiagnosticId,
            MemberExceptionsAnalyzer.AccessorDiagnosticId,
            MemberExceptionsAnalyzer.IteratorDiagnosticId,
        });

        public sealed override FixAllProvider? GetFixAllProvider() => null;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            if (!document.SupportsSyntaxTree || !document.SupportsSemanticModel || !document.Project.SupportsCompilation)
                return;

            var syntaxRoot = (await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;

            foreach (var diagnostic in context.Diagnostics)
            {
                // Diagnostic is located on a token in a statement or expression in the member body.
                var diagnosticSpan = diagnostic.Location.SourceSpan;
                var node = syntaxRoot.FindToken(diagnosticSpan.Start).Parent!;

                var compilation = (await document.Project.GetCompilationAsync(context.CancellationToken))!;

                string[] exceptionTypeIds = diagnostic.Properties[MemberExceptionsAnalyzer.PropertyKeys.ExceptionTypeIds]!.Split(',');
                var exceptionTypeIdsAndTypes = exceptionTypeIds
                    .Select(x => (ExceptionTypeId: x, ExceptionType: DocumentationCommentId.GetFirstSymbolForDeclarationId(x, compilation)))
                    .ToArray();

                string? memberId = diagnostic.Properties[MemberExceptionsAnalyzer.PropertyKeys.MemberId];

                string? accessor;
                _ = diagnostic.Properties.TryGetValue(MemberExceptionsAnalyzer.PropertyKeys.Accessor, out accessor);

                var declaration = Helpers.GetMemberDeclarationSyntax(node)!;

                if (diagnostic.Id != MemberExceptionsAnalyzer.IteratorDiagnosticId)
                {
                    context.RegisterCodeFix(
                        GetDocumentationCodeAction(document, syntaxRoot, declaration, memberId, accessor, exceptionTypeIdsAndTypes),
                        diagnostic);
                }

                string throwerMemberId;
                if (diagnostic.Properties.TryGetValue(MemberExceptionsAnalyzer.PropertyKeys.ThrowerMemberId, out throwerMemberId!))
                {
                    var throwerMember = DocumentationCommentId.GetFirstSymbolForDeclarationId(throwerMemberId, compilation);

                    if (throwerMember == null ||
                        !SymbolEqualityComparer.Default.Equals(throwerMember.ContainingAssembly, compilation.Assembly))
                    {
                        string? throwerAccessor;
                        _ = diagnostic.Properties.TryGetValue(MemberExceptionsAnalyzer.PropertyKeys.ThrowerAccessor, out throwerAccessor);

                        context.RegisterCodeFix(
                            Helpers.GetRemovalAdjustmentCodeAction(document, declaration, throwerMemberId, throwerMember, throwerAccessor, exceptionTypeIdsAndTypes),
                            diagnostic);
                    }
                }
            }
        }

        private static CodeAction GetDocumentationCodeAction(Document document, SyntaxNode syntaxRoot, SyntaxNode declaration, string? memberId, string? accessor, (string ExceptionTypeId, ISymbol ExceptionType)[] exceptionTypeIdsAndTypes)
        {
            var codeActions = exceptionTypeIdsAndTypes
                .Select(x =>
                {
                    string exceptionTypeId = x.ExceptionTypeId;
                    var exceptionType = x.ExceptionType;

                    string exceptionName = exceptionType != null
                        ? exceptionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                        : exceptionTypeId;

                    return CodeAction.Create(
                        title: $"Document '{exceptionName}'",
                        createChangedDocument: async cancellationToken =>
                        {
                            string cref;
                            if (exceptionType != null)
                            {
                                var semanticModel = (await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false))!;

                                cref = exceptionType.ToMinimalDisplayString(semanticModel, position: declaration.SpanStart).Replace('<', '{').Replace('>', '}');
                            }
                            else
                            {
                                cref = exceptionTypeId;
                            }

                            var newDeclaration = declaration.WithLeadingTrivia(
                                Helpers.AddExceptionXmlElement(
                                    declaration.GetLeadingTrivia(),
                                    cref,
                                    accessor));

                            var newSyntaxRoot = syntaxRoot.ReplaceNode(declaration, newDeclaration);

                            return document.WithSyntaxRoot(newSyntaxRoot);
                        },
                        equivalenceKey: $"{memberId} {accessor} {exceptionTypeId}");
                })
                .ToImmutableArray();

            return CodeAction.Create(
                title: "Document exceptions",
                nestedActions: codeActions,
                isInlinable: true);
        }
    }
}
