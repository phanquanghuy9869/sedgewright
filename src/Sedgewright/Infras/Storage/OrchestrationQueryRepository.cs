using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Aggregates orchestration snapshots from the table-specific repositories.
/// </summary>
public sealed class OrchestrationQueryRepository(
    ISequentialOrchestrationRepository sequentialRepository,
    IConcurrentOrchestrationRepository concurrentRepository,
    IGroupChatOrchestrationRepository groupChatRepository,
    IMagenticOrchestrationRepository magenticRepository,
    IHandoffOrchestrationRepository handoffRepository) : IOrchestrationQueryRepository
{
    private readonly ISequentialOrchestrationRepository _sequentialRepository = sequentialRepository;
    private readonly IConcurrentOrchestrationRepository _concurrentRepository = concurrentRepository;
    private readonly IGroupChatOrchestrationRepository _groupChatRepository = groupChatRepository;
    private readonly IMagenticOrchestrationRepository _magenticRepository = magenticRepository;
    private readonly IHandoffOrchestrationRepository _handoffRepository = handoffRepository;

    /// <inheritdoc />
    public async Task<List<OrchestrationListItemSnapshot>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<OrchestrationListItemSnapshot> items = [];

        items.AddRange((await _sequentialRepository.GetAllAsync(cancellationToken))
            .Select(orchestration => new OrchestrationListItemSnapshot(
                orchestration.Id,
                OrchestrationKind.Sequential,
                orchestration.Name,
                orchestration.Description ?? string.Empty,
                $"{orchestration.Steps.Count} steps")));

        items.AddRange((await _concurrentRepository.GetAllAsync(cancellationToken))
            .Select(orchestration => new OrchestrationListItemSnapshot(
                orchestration.Id,
                OrchestrationKind.Concurrent,
                orchestration.Name,
                orchestration.Description ?? string.Empty,
                $"{orchestration.Participants.Count} participants")));

        items.AddRange((await _groupChatRepository.GetAllAsync(cancellationToken))
            .Select(orchestration => new OrchestrationListItemSnapshot(
                orchestration.Id,
                OrchestrationKind.GroupChat,
                orchestration.Name,
                orchestration.Description ?? string.Empty,
                $"{orchestration.Participants.Count} participants - {orchestration.MaximumIterationCount} iterations")));

        items.AddRange((await _magenticRepository.GetAllAsync(cancellationToken))
            .Select(orchestration => new OrchestrationListItemSnapshot(
                orchestration.Id,
                OrchestrationKind.Magentic,
                orchestration.Name,
                orchestration.Description ?? string.Empty,
                $"{orchestration.Participants.Count} participants - {orchestration.MaximumRoundCount} rounds")));

        items.AddRange((await _handoffRepository.GetAllAsync(cancellationToken))
            .Select(orchestration => new OrchestrationListItemSnapshot(
                orchestration.Id,
                OrchestrationKind.Handoff,
                orchestration.Name,
                orchestration.Description ?? string.Empty,
                orchestration.IsAutonomousModeEnabled
                    ? $"{orchestration.Participants.Count} participants - autonomous"
                    : $"{orchestration.Participants.Count} participants - {orchestration.Routes.Count} routes")));

        return [.. items
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.Id)];
    }
}