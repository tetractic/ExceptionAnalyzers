// Copyright 2021 Carl Reinke
//
// This file is part of a library that is licensed under the terms of GNU Lesser
// General Public License version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Tetractic.CodeAnalysis.ExceptionAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed partial class MemberExceptionsAnalyzer : DocumentedExceptionsAnalyzerBase
    {
        public const string DiagnosticId = "Ex0100";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: "Member may throw undocumented exception",
            messageFormat: "'{0}' may throw undocumented exception: {1}",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public const string AccessorDiagnosticId = "Ex0101";

        internal static readonly DiagnosticDescriptor AccessorRule = new DiagnosticDescriptor(
            id: AccessorDiagnosticId,
            title: "Member accessor may throw undocumented exception",
            messageFormat: "'{0}' '{1}' accessor may throw undocumented exception: {2}",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public const string InitializerDiagnosticId = "Ex0104";

        internal static readonly DiagnosticDescriptor InitializerRule = new DiagnosticDescriptor(
            id: InitializerDiagnosticId,
            title: "Member initializer may throw undocumented exception",
            messageFormat: "'{0}' initializer may throw undocumented exception: {1}",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public const string DelegateDiagnosticId = "Ex0120";

        internal static readonly DiagnosticDescriptor DelegateCreationRule = new DiagnosticDescriptor(
            id: DelegateDiagnosticId,
            title: "Delegate created from member may throw undocumented exception",
            messageFormat: "Delegate '{0}' created from '{1}' may throw undocumented exception: {2}",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Hidden,
            isEnabledByDefault: true);

        public const string AnonymousDelegateDiagnosticId = "Ex0121";

        internal static readonly DiagnosticDescriptor AnonymousDelegateCreationRule = new DiagnosticDescriptor(
            id: AnonymousDelegateDiagnosticId,
            title: "Delegate created from anonymous function may throw undocumented exception",
            messageFormat: "Delegate '{0}' created from anonymous function may throw undocumented exception: {1}",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Hidden,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Rule,
            AccessorRule,
            InitializerRule,
            DelegateCreationRule,
            AnonymousDelegateCreationRule,
            ExceptionAdjustmentsFileAnalyzer.SyntaxErrorRule,
            ExceptionAdjustmentsFileAnalyzer.SymbolRule,
        });

        public override void Initialize(AnalysisContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var compilation = compilationStartContext.Compilation;
                var additionalFiles = compilationStartContext.Options.AdditionalFiles;
                var cancellationToken = compilationStartContext.CancellationToken;

                var exceptionAdjustments = ExceptionAdjustments.Load(additionalFiles, cancellationToken);
                var documentedExceptionTypesProvider = GetOrCreateDocumentedExceptionTypesProvider(compilation, exceptionAdjustments);

                var contextCache = new ConcurrentDictionary<AnalyzerConfigOptions, Context>();

                compilationStartContext.RegisterSemanticModelAction(semanticModelContext => AnalyzeSemanticModel(semanticModelContext, documentedExceptionTypesProvider, contextCache));
            });
        }

        private static void AnalyzeSemanticModel(
            SemanticModelAnalysisContext semanticModelContext,
            DocumentedExceptionTypesProvider documentedExceptionTypesProvider,
            ConcurrentDictionary<AnalyzerConfigOptions, Context> contextCache)
        {
            var semanticModel = semanticModelContext.SemanticModel;
            var syntaxTree = semanticModel.SyntaxTree;
            var compilation = semanticModel.Compilation;
            var cancellationToken = semanticModelContext.CancellationToken;

            var options = semanticModelContext.Options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
            var context = GetOrCreateContext(contextCache, options, documentedExceptionTypesProvider);

            var visitor = new MemberVisitor(semanticModelContext, context);

            var root = syntaxTree.GetRoot(cancellationToken);

            foreach (var node in root.DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (node.Kind())
                {
                    case SyntaxKind.FieldDeclaration:
                    {
                        var fieldSyntax = (FieldDeclarationSyntax)node;

                        visitor.Analyze(fieldSyntax);
                        break;
                    }

                    case SyntaxKind.MethodDeclaration:
                    case SyntaxKind.OperatorDeclaration:
                    case SyntaxKind.ConversionOperatorDeclaration:
                    case SyntaxKind.DestructorDeclaration:
                    {
                        var baseMethodSyntax = (BaseMethodDeclarationSyntax)node;

                        visitor.Analyze(baseMethodSyntax);
                        break;
                    }

                    case SyntaxKind.ConstructorDeclaration:
                    {
                        var constructorSyntax = (ConstructorDeclarationSyntax)node;

                        visitor.Analyze(constructorSyntax);
                        break;
                    }

                    case SyntaxKind.PropertyDeclaration:
                    case SyntaxKind.EventDeclaration:
                    case SyntaxKind.IndexerDeclaration:
                    {
                        var basePropertySyntax = (BasePropertyDeclarationSyntax)node;

                        visitor.Analyze(basePropertySyntax);
                        break;
                    }
                }
            }
        }

        internal static class PropertyKeys
        {
            public const string ExceptionTypeIds = nameof(ExceptionTypeIds);

            public const string MemberId = nameof(MemberId);

            public const string Accessor = nameof(Accessor);

            public const string ThrowerMemberId = nameof(ThrowerMemberId);

            public const string ThrowerAccessor = nameof(ThrowerAccessor);
        }
    }
}
