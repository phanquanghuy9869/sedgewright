using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Loads the raw chat targets for the modal chat target picker.
/// </summary>
public sealed class ChatTargetCatalogService(
    IAgentDefinitionRepository agentRepository,
    ISequentialOrchestrationRepository sequentialOrchestrationRepository,
    IConcurrentOrchestrationRepository concurrentOrchestrationRepository,
    IGroupChatOrchestrationRepository groupChatOrchestrationRepository,
    IMagenticOrchestrationRepository magenticOrchestrationRepository,
    IHandoffOrchestrationRepository handoffOrchestrationRepository) : IChatTargetCatalogService
{
    private readonly IAgentDefinitionRepository _agentRepository = agentRepository;
    private readonly ISequentialOrchestrationRepository _sequentialOrchestrationRepository = sequentialOrchestrationRepository;
    private readonly IConcurrentOrchestrationRepository _concurrentOrchestrationRepository = concurrentOrchestrationRepository;
    private readonly IGroupChatOrchestrationRepository _groupChatOrchestrationRepository = groupChatOrchestrationRepository;
    private readonly IMagenticOrchestrationRepository _magenticOrchestrationRepository = magenticOrchestrationRepository;
    private readonly IHandoffOrchestrationRepository _handoffOrchestrationRepository = handoffOrchestrationRepository;

    /// <inheritdoc />
    public async Task<IReadOnlyList<IChatTarget>> GetTargetsAsync(CancellationToken cancellationToken = default)
    {
        List<IChatTarget> targets = [];

        List<AgentDefinition> agents = await _agentRepository.GetAllAsync(cancellationToken);
        targets.AddRange(agents.OrderBy(agent => agent.Name));

        List<SequentialOrchestration> sequentialOrchestrations = await _sequentialOrchestrationRepository.GetAllAsync(cancellationToken);
        targets.AddRange(sequentialOrchestrations.OrderBy(orchestration => orchestration.Name));

        List<ConcurrentOrchestration> concurrentOrchestrations = await _concurrentOrchestrationRepository.GetAllAsync(cancellationToken);
        targets.AddRange(concurrentOrchestrations.OrderBy(orchestration => orchestration.Name));

        List<GroupChatOrchestration> groupChatOrchestrations = await _groupChatOrchestrationRepository.GetAllAsync(cancellationToken);
        targets.AddRange(groupChatOrchestrations.OrderBy(orchestration => orchestration.Name));

        List<MagenticOrchestration> magenticOrchestrations = await _magenticOrchestrationRepository.GetAllAsync(cancellationToken);
        targets.AddRange(magenticOrchestrations.OrderBy(orchestration => orchestration.Name));

        List<HandoffOrchestration> handoffOrchestrations = await _handoffOrchestrationRepository.GetAllAsync(cancellationToken);
        targets.AddRange(handoffOrchestrations.OrderBy(orchestration => orchestration.Name));

        return targets;
    }
}
