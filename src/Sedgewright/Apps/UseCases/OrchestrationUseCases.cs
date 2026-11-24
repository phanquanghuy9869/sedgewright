using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Models;
using Sedgewright.Infras.Services;
using Sedgewright.Entities;

namespace Sedgewright.Apps.UseCases;

public sealed class OrchestrationUseCases(IWorkflowExecutionStrategyFactory strategyFactory)
{
    public Task<UnitResult<IApplicationError>> Execute(
        IOrchestration orchestration,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        CancellationToken cancellationToken = default) =>
        strategyFactory
            .GetStrategy(orchestration.GetType())
            .Bind(strategy => strategy.Execute(orchestration, prompt, onIntermediateUpdate, onFinalOutput, cancellationToken));
}
