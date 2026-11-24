using System;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.Services;

public interface IWorkflowExecutionStrategyFactory
{
    Result<IWorkflowExecutionStrategy, IApplicationError> GetStrategy<T>() where T : IOrchestration;

    Result<IWorkflowExecutionStrategy, IApplicationError> GetStrategy(Type orchestrationType);
}
