// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SupertypeExceptionsCodeFixProvider)), Shared]
    public sealed class SupertypeExceptionsCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(new[]
        {
            SupertypeExceptionsAnalyzer.DiagnosticId,
            SupertypeExceptionsAnalyzer.AccessorDiagnosticId,
        });

        public sealed override FixAllProvider? GetFixAllProvider() => null;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            if (!document.SupportsSyntaxTree || !document.Project.SupportsCompilation)
                return;

            var syntaxRoot = (await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false))!;

            foreach (var diagnostic in context.Diagnostics)
            {
                var compilation = (await document.Project.GetCompilationAsync(context.CancellationToken))!;

                string[] exceptionTypeIds = diagnostic.Properties[SupertypeExceptionsAnalyzer.PropertyKeys.ExceptionTypeIds]!.Split(',');
                var exceptionTypeIdsAndTypes = exceptionTypeIds
                    .Select(x => (ExceptionTypeId: x, ExceptionType: DocumentationCommentId.GetFirstSymbolForDeclarationId(x, compilation)))
                    .ToArray();

                string supertypeMemberId;
                if (diagnostic.Properties.TryGetValue(SupertypeExceptionsAnalyzer.PropertyKeys.SupertypeMemberId, out supertypeMemberId!))
                {
                    var supertypeMember = DocumentationCommentId.GetFirstSymbolForDeclarationId(supertypeMemberId, compilation);

                    if (supertypeMember == null ||
                        !SymbolEqualityComparer.Default.Equals(supertypeMember.ContainingAssembly, compilation.Assembly))
                    {
                        string? supertypeAccessor;
                        _ = diagnostic.Properties.TryGetValue(SupertypeExceptionsAnalyzer.PropertyKeys.SupertypeAccessor, out supertypeAccessor);

                        context.RegisterCodeFix(
                            Helpers.GetAdditionAdjustmentCodeAction(document, supertypeMemberId, supertypeMember, supertypeAccessor, exceptionTypeIdsAndTypes),
                            diagnostic);
                    }
                }
            }
        }
    }
}
