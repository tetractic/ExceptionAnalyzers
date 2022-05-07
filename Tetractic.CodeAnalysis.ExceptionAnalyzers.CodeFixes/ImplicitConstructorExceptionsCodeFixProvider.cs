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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ImplicitConstructorExceptionsCodeFixProvider)), Shared]
    public sealed class ImplicitConstructorExceptionsCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(ImplicitConstructorExceptionsAnalyzer.DiagnosticId);

        public sealed override FixAllProvider? GetFixAllProvider() => null;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            if (!document.SupportsSyntaxTree || !document.SupportsSemanticModel || !document.Project.SupportsCompilation)
                return;

            var syntaxRoot = (await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;

            foreach (var diagnostic in context.Diagnostics)
            {
                // Diagnostic is located on the ClassDeclarationSyntax.Identifier token.
                var diagnosticSpan = diagnostic.Location.SourceSpan;
                var node = syntaxRoot.FindToken(diagnosticSpan.Start).Parent!;

                var compilation = (await document.Project.GetCompilationAsync(context.CancellationToken))!;

                string[] exceptionTypeIds = diagnostic.Properties[ImplicitConstructorExceptionsAnalyzer.PropertyKeys.ExceptionTypeIds]!.Split(',');
                var exceptionTypeIdsAndTypes = exceptionTypeIds
                    .Select(x => (ExceptionTypeId: x, ExceptionType: DocumentationCommentId.GetFirstSymbolForDeclarationId(x, compilation)))
                    .ToArray();

                string classId = diagnostic.Properties[ImplicitConstructorExceptionsAnalyzer.PropertyKeys.ClassId]!;

                var classDeclaration = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();

                var codeActions = exceptionTypeIdsAndTypes
                    .Select(x =>
                    {
                        string exceptionTypeId = x.ExceptionTypeId;
                        var exceptionType = x.ExceptionType;

                        string exceptionName = exceptionType != null
                            ? exceptionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                            : exceptionTypeId;

                        return CodeAction.Create(
                            title: $"Document '{exceptionName}' on constructor",
                            createChangedDocument: async cancellationToken =>
                            {
                                string cref;
                                if (exceptionType != null)
                                {
                                    var semanticModel = (await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false))!;

                                    cref = exceptionType.ToMinimalDisplayString(semanticModel, position: classDeclaration.OpenBraceToken.Span.End).Replace('<', '{').Replace('>', '}');
                                }
                                else
                                {
                                    cref = exceptionTypeId;
                                }

                                var constructorDeclaration = SyntaxFactory.ConstructorDeclaration(
                                    attributeLists: default,
                                    modifiers: SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)),
                                    identifier: classDeclaration.Identifier,
                                    parameterList: SyntaxFactory.ParameterList(),
                                    initializer: default!,
                                    body: SyntaxFactory.Block());

                                constructorDeclaration = constructorDeclaration.WithLeadingTrivia(
                                    Helpers.AddExceptionXmlElement(
                                        constructorDeclaration.GetLeadingTrivia(),
                                        cref,
                                        accessor: null));

                                var newClassDeclaration = classDeclaration.WithMembers(
                                    classDeclaration.Members.Insert(0, constructorDeclaration));

                                var newSyntaxRoot = syntaxRoot.ReplaceNode(classDeclaration, newClassDeclaration);

                                return document.WithSyntaxRoot(newSyntaxRoot);
                            },
                            equivalenceKey: $"{classId} {exceptionTypeId}");
                    })
                    .ToImmutableArray();

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Document exceptions on constructor",
                        nestedActions: codeActions,
                        isInlinable: true),
                    diagnostic);

                string throwerMemberId = diagnostic.Properties[ImplicitConstructorExceptionsAnalyzer.PropertyKeys.ThrowerMemberId]!;

                var throwerMember = DocumentationCommentId.GetFirstSymbolForDeclarationId(throwerMemberId, compilation);

                context.RegisterCodeFix(
                    Helpers.GetRemovalAdjustmentCodeAction(document, throwerMemberId, throwerMember, null, exceptionTypeIdsAndTypes),
                    diagnostic);
            }
        }
    }
}
