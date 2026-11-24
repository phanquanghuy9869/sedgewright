using System;
using Sedgewright.Entities;

namespace Sedgewright.Models;

/// <summary>
/// Identifies a chat target by concrete type and stable id.
/// </summary>
public sealed record ChatTargetIdentity(ChatTargetType TargetType, int Id)
{
    /// <summary>
    /// Builds an identity for a chat target.
    /// </summary>
    public static ChatTargetIdentity From(IChatTarget target)
        => target switch
        {
            AgentDefinition agent => new(ChatTargetType.Agent, agent.Id),
            SequentialOrchestration orchestration => new(ChatTargetType.SequentialOrchestration, orchestration.Id),
            ConcurrentOrchestration orchestration => new(ChatTargetType.ConcurrentOrchestration, orchestration.Id),
            GroupChatOrchestration orchestration => new(ChatTargetType.GroupChatOrchestration, orchestration.Id),
            MagenticOrchestration orchestration => new(ChatTargetType.MagenticOrchestration, orchestration.Id),
            HandoffOrchestration orchestration => new(ChatTargetType.HandoffOrchestration, orchestration.Id),
            _ => throw new NotSupportedException($"Unsupported chat target type: {target.GetType().Name}"),
        };

    /// <summary>
    /// Checks whether the identity matches the provided target.
    /// </summary>
    public bool Matches(IChatTarget target)
        => target switch
        {
            AgentDefinition agent => TargetType == ChatTargetType.Agent && Id == agent.Id,
            SequentialOrchestration orchestration => TargetType == ChatTargetType.SequentialOrchestration && Id == orchestration.Id,
            ConcurrentOrchestration orchestration => TargetType == ChatTargetType.ConcurrentOrchestration && Id == orchestration.Id,
            GroupChatOrchestration orchestration => TargetType == ChatTargetType.GroupChatOrchestration && Id == orchestration.Id,
            MagenticOrchestration orchestration => TargetType == ChatTargetType.MagenticOrchestration && Id == orchestration.Id,
            HandoffOrchestration orchestration => TargetType == ChatTargetType.HandoffOrchestration && Id == orchestration.Id,
            _ => false,
        };
}
