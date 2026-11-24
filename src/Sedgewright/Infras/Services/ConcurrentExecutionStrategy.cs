using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI.Workflows;
using Sedgewright.Entities;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Models;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Executes a concurrent orchestration through Microsoft Agent Framework workflows.
/// </summary>
public sealed class ConcurrentExecutionStrategy : GenericWorkflowExecutionStrategy<ConcurrentOrchestration>
{
    public override Task<UnitResult<IApplicationError>> Execute(
        ConcurrentOrchestration orchestration,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        CancellationToken cancellationToken = default) =>
            Workflow
                .From(orchestration)
                .Bind(workflow => workflow.ExecuteConcurrentAsync(prompt, onIntermediateUpdate, onFinalOutput, cancellationToken));
}
