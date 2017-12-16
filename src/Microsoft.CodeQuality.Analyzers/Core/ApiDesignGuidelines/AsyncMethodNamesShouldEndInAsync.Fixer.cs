// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.Rename;
using Analyzer.Utilities;
using System.Threading;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CodeActions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// Async002: Async Method Names Should End in Async
    /// </summary>
    public abstract class AsyncMethodNamesShouldEndInAsyncFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AsyncMethodNamesShouldEndInAsyncAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            Document document = context.Document;
            var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var method = (IMethodSymbol)semanticModel.GetEnclosingSymbol(diagnosticSpan.Start, context.CancellationToken);

            string oldMethodName = method.Name;
            string newMethodName = GetMethodNameWithAsyncSuffix(oldMethodName);
            Debug.Assert(oldMethodName != newMethodName);

            string title = string.Format(
                MicrosoftApiDesignGuidelinesAnalyzersResources.AsyncMethodNamesShouldEndInAsyncTitle,
                oldMethodName,
                newMethodName);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedSolution: async ct => await RenameMethodAsync(document, method, newMethodName, semanticModel, ct).ConfigureAwait(false),
                    equivalenceKey: title),
                diagnostic);
        }

        private static string GetMethodNameWithAsyncSuffix(string methodName)
        {
            if (HasMisspelledAsyncSuffix(methodName))
            {
                Debug.Assert(methodName.Length >= 5);
                var prefix = methodName.Substring(0, methodName.Length - 5);
                return $"{prefix}Async";
            }

            return $"{methodName}Async";
        }

        /// <summary>
        /// Decides whether <paramref name="methodName"/> has a 5-letter suffix that is likely a misspelled version of 'Async', such as 'Asycn' or 'async'.
        /// </summary>
        private static bool HasMisspelledAsyncSuffix(string methodName)
        {
            if (methodName.Length < 5)
            {
                return false;
            }

            var suffix = methodName.Substring(methodName.Length - 5);
            return suffix.ToUpperInvariant().IsAnagramTo(methodName.ToUpperInvariant());
        }

        private async Task<Solution> RenameMethodAsync(
            Document document,
            IMethodSymbol method,
            string newMethodName,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var oldSolution = document.Project.Solution;
            return await Renamer.RenameSymbolAsync(
                oldSolution,
                method,
                newMethodName,
                oldSolution.Workspace.Options,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
 