using System;
using CSharpFunctionalExtensions;
using Sedgewright.Models;

namespace Sedgewright.Entities;

public enum ToolDefinitionType
{
    Workflow,
    Native,
}

public sealed class ToolDefinition
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? ToolDescription { get; set; }

    public ToolDefinitionType Type { get; set; }

    public int? SequentialOrchestrationId { get; set; }

    public SequentialOrchestration? SequentialOrchestration { get; set; }

    public int? ConcurrentOrchestrationId { get; set; }

    public ConcurrentOrchestration? ConcurrentOrchestration { get; set; }

    public int? GroupChatOrchestrationId { get; set; }

    public GroupChatOrchestration? GroupChatOrchestration { get; set; }

    public int? MagenticOrchestrationId { get; set; }

    public MagenticOrchestration? MagenticOrchestration { get; set; }

    public int? HandoffOrchestrationId { get; set; }

    public HandoffOrchestration? HandoffOrchestration { get; set; }

    /// <summary>
    /// Returns the orchestration associated with this tool definition.
    /// </summary>
    public Result<IOrchestration, IApplicationError> GetOrchestration()
    {
        IOrchestration? orchestration = (IOrchestration?)SequentialOrchestration
            ?? (IOrchestration?)ConcurrentOrchestration
            ?? (IOrchestration?)GroupChatOrchestration
            ?? (IOrchestration?)MagenticOrchestration
            ?? HandoffOrchestration;

        return orchestration is not null
            ? Result.Success<IOrchestration, IApplicationError>(orchestration)
            : Result.Failure<IOrchestration, IApplicationError>(
                new InvalidOperationError(["The tool definition has no associated orchestration."]));
    }
}
