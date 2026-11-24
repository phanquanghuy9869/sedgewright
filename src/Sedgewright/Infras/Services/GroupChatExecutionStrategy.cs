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
/// Executes a group chat orchestration through Microsoft Agent Framework workflows.
/// </summary>
public sealed class GroupChatExecutionStrategy : GenericWorkflowExecutionStrategy<GroupChatOrchestration>
{
    public override Task<UnitResult<IApplicationError>> Execute(
        GroupChatOrchestration orchestration,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        CancellationToken cancellationToken = default) =>
            Workflow
                .From(orchestration)
                .Bind(workflow => workflow.ExecuteGroupChatAsync(prompt, onIntermediateUpdate, onFinalOutput, cancellationToken));
}
