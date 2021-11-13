// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MemberExceptionsCodeFixProvider)), Shared]
    public sealed class MemberInitializerExceptionsCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(new[]
        {
            MemberExceptionsAnalyzer.InitializerDiagnosticId,
        });

        public sealed override FixAllProvider? GetFixAllProvider() => null;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var syntaxRoot = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (var diagnostic in context.Diagnostics)
            {
                var diagnosticSpan = diagnostic.Location.SourceSpan;
                var node = syntaxRoot.FindToken(diagnosticSpan.Start).Parent;

                var compilation = (await document.Project.GetCompilationAsync(context.CancellationToken))!;

                string[] exceptionTypeIds = diagnostic.Properties[MemberExceptionsAnalyzer.PropertyKeys.ExceptionTypeIds].Split(',');
                var exceptionTypeIdsAndTypes = exceptionTypeIds
                    .Select(x => (ExceptionTypeId: x, ExceptionType: DocumentationCommentId.GetFirstSymbolForDeclarationId(x, compilation)))
                    .ToArray();

                var declaration = Helpers.GetMemberDeclarationSyntax(node)!;

                string throwerMemberId;
                if (diagnostic.Properties.TryGetValue(MemberExceptionsAnalyzer.PropertyKeys.ThrowerMemberId, out throwerMemberId))
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
    }
}
