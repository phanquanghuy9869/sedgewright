using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.Services;

public sealed class WorkflowExecutionStrategyFactory(IEnumerable<IWorkflowExecutionStrategy> strategies) : IWorkflowExecutionStrategyFactory
{
    private readonly Dictionary<Type, IWorkflowExecutionStrategy> _strategies = strategies.ToDictionary(s => s.OrchestrationType);

    public Result<IWorkflowExecutionStrategy, IApplicationError> GetStrategy(Type orchestrationType)
    {
        if (_strategies.TryGetValue(orchestrationType, out IWorkflowExecutionStrategy? strategy))
        {
            return Result.Success<IWorkflowExecutionStrategy, IApplicationError>(strategy);
        }

        return new InvalidOperationError([$"No strategy found for orchestration type {orchestrationType.Name}"]);
    }

    public Result<IWorkflowExecutionStrategy, IApplicationError> GetStrategy<T>() where T : IOrchestration =>
        GetStrategy(typeof(T));
}
