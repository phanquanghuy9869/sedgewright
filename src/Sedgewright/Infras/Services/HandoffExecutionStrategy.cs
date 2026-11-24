using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.Infras.MAF.Extensions;
using Microsoft.Agents.AI.Workflows;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Executes a handoff orchestration through the Microsoft Agent Framework workflow runtime.
/// </summary>
public sealed class HandoffExecutionStrategy : GenericWorkflowExecutionStrategy<HandoffOrchestration>
{
    public override Task<UnitResult<IApplicationError>> Execute(
        HandoffOrchestration orchestration,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        CancellationToken cancellationToken = default) =>
            Workflow
                .From(orchestration)
                .Bind(workflow => workflow.ExecuteHandoffAsync(prompt, onIntermediateUpdate, onFinalOutput, cancellationToken));
}
