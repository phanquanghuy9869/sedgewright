using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.Services;

public abstract class GenericWorkflowExecutionStrategy<T> : IWorkflowExecutionStrategy, IWorkflowExecutionStrategy<T> where T : IOrchestration
{
    public Type OrchestrationType => typeof(T);
    public Task<UnitResult<IApplicationError>> Execute(
        IOrchestration orchestration,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        CancellationToken cancellationToken = default) => Execute((T)orchestration, prompt, onIntermediateUpdate, onFinalOutput, cancellationToken);

    public abstract Task<UnitResult<IApplicationError>> Execute(
        T orchestration,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        CancellationToken cancellationToken = default);
}
