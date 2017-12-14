// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// Async006: Don't Mix Blocking and Async
    /// </summary>
    public abstract class DoNotMixBlockingAndAsyncAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "Async006";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftApiDesignGuidelinesAnalyzersResources.DoNotMixBlockingAndAsyncTitle), MicrosoftApiDesignGuidelinesAnalyzersResources.ResourceManager, typeof(MicrosoftApiDesignGuidelinesAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftApiDesignGuidelinesAnalyzersResources.DoNotMixBlockingAndAsyncMessage), MicrosoftApiDesignGuidelinesAnalyzersResources.ResourceManager, typeof(MicrosoftApiDesignGuidelinesAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftApiDesignGuidelinesAnalyzersResources.DoNotMixBlockingAndAsyncDescription), MicrosoftApiDesignGuidelinesAnalyzersResources.ResourceManager, typeof(MicrosoftApiDesignGuidelinesAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Usage,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: false,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: null,     // TODO: add MSDN url
                                                                             customTags: WellKnownDiagnosticTags.Telemetry);

        // NOTE: The awaiter pattern is duck-typed. After checking for membership in this set, check for GetAwaiter().GetResult() separately.
        private static readonly ImmutableHashSet<string> s_blockingMethodNames = new[]
        {
            "System.Threading.Tasks.Task.Wait",
            "System.Threading.Tasks.Task.WaitAll",
            "System.Threading.Tasks.Task.WaitAny",
            "System.Threading.Thread.Sleep",
        }
        .ToImmutableHashSet();

        private static readonly ImmutableHashSet<string> s_blockingPropertyNames = new[]
        {
            "System.Threading.Tasks.Task`1.Result"
        }
        .ToImmutableHashSet();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation, OperationKind.PropertyReference);
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var operation = context.Operation;
            if (!IsBlockingCall(operation))
            {
                return;
            }

            var semanticModel = context.Compilation.GetSemanticModel(operation.Syntax.SyntaxTree);
            var containingMethod = (IMethodSymbol)semanticModel.GetEnclosingSymbol(operation.Syntax.SpanStart, context.CancellationToken);

            if (containingMethod == null || !containingMethod.IsAsync)
            {
                return;
            }

            // TODO: Add arguments.
            context.ReportDiagnostic(operation.Syntax.CreateDiagnostic(Rule));
        }

        private static bool IsBlockingCall(IOperation operation)
        {
            switch (operation.Kind)
            {
                case OperationKind.Invocation:
                    var invocation = (IInvocationOperation)operation;
                    var methodName = invocation.TargetMethod?.MetadataName;
                    if (methodName == null)
                    {
                        break;
                    }

                    if (s_blockingMethodNames.Contains(methodName))
                    {
                        return true;
                    }

                    // Check for duck-typed GetAwaiter().GetResult() pattern.
                    if (invocation.TargetMethod.Name == "GetResult" &&
                        invocation.Instance is IInvocationOperation awaiterOperation &&
                        awaiterOperation.TargetMethod?.Name == "GetAwaiter")
                    {
                        return true;
                    }

                    break;
                case OperationKind.PropertyReference:
                    var propertyRef = (IPropertyReferenceOperation)operation;
                    var propertyName = propertyRef.Property?.MetadataName;
                    if (propertyName != null && s_blockingPropertyNames.Contains(propertyName))
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }
    }
}