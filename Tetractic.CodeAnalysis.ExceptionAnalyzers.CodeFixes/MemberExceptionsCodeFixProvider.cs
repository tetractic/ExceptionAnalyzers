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
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
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
        });

        public sealed override FixAllProvider GetFixAllProvider() => null;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var syntaxRoot = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (var diagnostic in context.Diagnostics)
            {
                if (diagnostic.Descriptor.GetType().Assembly != typeof(MemberExceptionsAnalyzer).Assembly)
                {
                    Debug.Assert(false, "Diagnostic ID collision.");
                    continue;
                }

                var diagnosticSpan = diagnostic.Location.SourceSpan;
                var node = syntaxRoot.FindToken(diagnosticSpan.Start).Parent;

                var compilation = await document.Project.GetCompilationAsync(context.CancellationToken);

                string[] exceptionTypeIds = diagnostic.Properties[MemberExceptionsAnalyzer.PropertyKeys.ExceptionTypeIds].Split(',');
                var exceptionTypeIdsAndTypes = exceptionTypeIds
                    .Select(x => (ExceptionTypeId: x, ExceptionType: DocumentationCommentId.GetFirstSymbolForDeclarationId(x, compilation)))
                    .ToArray();

                string memberId = diagnostic.Properties[MemberExceptionsAnalyzer.PropertyKeys.MemberId];

                string accessor;
                _ = diagnostic.Properties.TryGetValue(MemberExceptionsAnalyzer.PropertyKeys.Accessor, out accessor);

                var declaration = GetDeclaration(node);

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
                                    var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

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

                                syntaxRoot = syntaxRoot.ReplaceNode(declaration, newDeclaration);

                                return document.WithSyntaxRoot(syntaxRoot);
                            },
                            equivalenceKey: $"{memberId} {accessor} {exceptionTypeId}");
                    })
                    .ToImmutableArray();

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Document exceptions",
                        nestedActions: codeActions,
                        isInlinable: true),
                    diagnostic);

                string throwerMemberId;
                if (diagnostic.Properties.TryGetValue(MemberExceptionsAnalyzer.PropertyKeys.ThrowerMemberId, out throwerMemberId))
                {
                    var throwerMember = DocumentationCommentId.GetFirstSymbolForDeclarationId(throwerMemberId, compilation);

                    if (throwerMember == null ||
                        !SymbolEqualityComparer.Default.Equals(throwerMember.ContainingAssembly, compilation.Assembly))
                    {
                        string throwerAccessor;
                        _ = diagnostic.Properties.TryGetValue(MemberExceptionsAnalyzer.PropertyKeys.ThrowerAccessor, out throwerAccessor);

                        context.RegisterCodeFix(
                            Helpers.GetRemovalAdjustmentCodeAction(document, throwerMemberId, throwerMember, throwerAccessor, exceptionTypeIdsAndTypes),
                            diagnostic);
                    }
                }
            }

            SyntaxNode GetDeclaration(SyntaxNode node)
            {
                foreach (var ancestorOrSelfNode in node.AncestorsAndSelf())
                {
                    switch (ancestorOrSelfNode.Kind())
                    {
                        case SyntaxKind.LocalFunctionStatement:
                        case SyntaxKind.MethodDeclaration:
                        case SyntaxKind.OperatorDeclaration:
                        case SyntaxKind.ConversionOperatorDeclaration:
                        case SyntaxKind.ConstructorDeclaration:
                        case SyntaxKind.DestructorDeclaration:
                        case SyntaxKind.PropertyDeclaration:
                        case SyntaxKind.EventDeclaration:
                        case SyntaxKind.IndexerDeclaration:
                            return ancestorOrSelfNode;
                    }
                }

                return null;
            }
        }
    }
}
