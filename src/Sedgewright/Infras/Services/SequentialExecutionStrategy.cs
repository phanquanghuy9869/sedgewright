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

public sealed class SequentialExecutionStrategy : GenericWorkflowExecutionStrategy<SequentialOrchestration>
{
    public override Task<UnitResult<IApplicationError>> Execute(
        SequentialOrchestration orchestration,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        CancellationToken cancellationToken = default) =>
            Workflow
                .From(orchestration)
                .Bind(workflow => workflow.ExecuteSequentialAsync(prompt, onIntermediateUpdate, onFinalOutput, cancellationToken));
}
