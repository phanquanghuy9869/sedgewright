using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

public interface IWorkflowExecutionStrategy
{
    public Type OrchestrationType { get; }
    Task<UnitResult<IApplicationError>> Execute(
        IOrchestration orchestration,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowExecutionStrategy<T> where T : IOrchestration
{
    public Task<UnitResult<IApplicationError>> Execute(
        T orchestration,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        CancellationToken cancellationToken = default);
}
