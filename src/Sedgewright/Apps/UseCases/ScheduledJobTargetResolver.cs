using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Apps.UseCases;

/// <summary>
/// Resolves the agent or orchestration a <see cref="ScheduledJob"/> points at, with its runtime graph loaded.
/// </summary>
public sealed class ScheduledJobTargetResolver(
    IAgentDefinitionRepository agentRepository,
    ISequentialOrchestrationRepository sequentialOrchestrationRepository,
    IConcurrentOrchestrationRepository concurrentOrchestrationRepository,
    IGroupChatOrchestrationRepository groupChatOrchestrationRepository,
    IMagenticOrchestrationRepository magenticOrchestrationRepository,
    IHandoffOrchestrationRepository handoffOrchestrationRepository)
{
    /// <summary>
    /// Loads the target identified by <paramref name="targetType"/> and <paramref name="targetId"/>,
    /// or returns <see langword="null"/> when it no longer exists.
    /// </summary>
    public async Task<IChatTarget?> ResolveAsync(
        ChatTargetType targetType,
        int targetId,
        CancellationToken cancellationToken = default)
        => targetType switch
        {
            ChatTargetType.Agent => await agentRepository.GetAsync(targetId, cancellationToken).ConfigureAwait(false),
            ChatTargetType.SequentialOrchestration => await sequentialOrchestrationRepository.GetAsync(targetId, cancellationToken).ConfigureAwait(false),
            ChatTargetType.ConcurrentOrchestration => await concurrentOrchestrationRepository.GetAsync(targetId, cancellationToken).ConfigureAwait(false),
            ChatTargetType.GroupChatOrchestration => await groupChatOrchestrationRepository.GetAsync(targetId, cancellationToken).ConfigureAwait(false),
            ChatTargetType.MagenticOrchestration => await magenticOrchestrationRepository.GetAsync(targetId, cancellationToken).ConfigureAwait(false),
            ChatTargetType.HandoffOrchestration => await handoffOrchestrationRepository.GetAsync(targetId, cancellationToken).ConfigureAwait(false),
            _ => null,
        };
}
