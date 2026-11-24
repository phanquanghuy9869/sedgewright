using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Sedgewright.Entities;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Models;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Executes a Magentic orchestration through Microsoft Agent Framework workflows.
/// </summary>
public sealed class MagenticExecutionStrategy(ILogger<MagenticExecutionStrategy> logger) : GenericWorkflowExecutionStrategy<MagenticOrchestration>
{
    public override Task<UnitResult<IApplicationError>> Execute(
        MagenticOrchestration orchestration,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        CancellationToken cancellationToken = default) =>
            Workflow
                .From(orchestration)
                .Bind(workflow => workflow.ExecuteMagenticAsync(prompt, onIntermediateUpdate, onFinalOutput, cancellationToken, logger));
}
