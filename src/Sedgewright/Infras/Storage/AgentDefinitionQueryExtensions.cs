using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

internal static class AgentDefinitionQueryExtensions
{
    public static IQueryable<AgentDefinition> IncludeRuntimeGraph(this IQueryable<AgentDefinition> query)
        => query
            .Include(definition => definition.Provider)
            .Include(definition => definition.Tools)
            .ThenInclude(tool => tool.ToolDefinition)
            .Include(definition => definition.Tools)
            .ThenInclude(tool => tool.ToolDefinition)
            .ThenInclude(tool => tool.SequentialOrchestration)
            .ThenInclude(orchestration => orchestration!.Steps)
            .ThenInclude(step => step.AgentDefinition)
            .ThenInclude(agent => agent.Provider)
            .Include(definition => definition.Tools)
            .ThenInclude(tool => tool.ToolDefinition)
            .ThenInclude(tool => tool.ConcurrentOrchestration)
            .ThenInclude(orchestration => orchestration!.Participants)
            .ThenInclude(participant => participant.AgentDefinition)
            .ThenInclude(agent => agent.Provider)
            .Include(definition => definition.Tools)
            .ThenInclude(tool => tool.ToolDefinition)
            .ThenInclude(tool => tool.GroupChatOrchestration)
            .ThenInclude(orchestration => orchestration!.Participants)
            .ThenInclude(participant => participant.AgentDefinition)
            .ThenInclude(agent => agent.Provider)
            .Include(definition => definition.Tools)
            .ThenInclude(tool => tool.ToolDefinition)
            .ThenInclude(tool => tool.MagenticOrchestration)
            .ThenInclude(orchestration => orchestration!.ManagerAgentDefinition)
            .ThenInclude(agent => agent.Provider)
            .Include(definition => definition.Tools)
            .ThenInclude(tool => tool.ToolDefinition)
            .ThenInclude(tool => tool.MagenticOrchestration)
            .ThenInclude(orchestration => orchestration!.Participants)
            .ThenInclude(participant => participant.AgentDefinition)
            .ThenInclude(agent => agent.Provider)
            .Include(definition => definition.Tools)
            .ThenInclude(tool => tool.ToolDefinition)
            .ThenInclude(tool => tool.HandoffOrchestration)
            .ThenInclude(orchestration => orchestration!.StartAgentDefinition)
            .ThenInclude(agent => agent.Provider)
            .Include(definition => definition.Tools)
            .ThenInclude(tool => tool.ToolDefinition)
            .ThenInclude(tool => tool.HandoffOrchestration)
            .ThenInclude(orchestration => orchestration!.Participants)
            .ThenInclude(participant => participant.AgentDefinition)
            .ThenInclude(agent => agent.Provider)
            .Include(definition => definition.Tools)
            .ThenInclude(tool => tool.ToolDefinition)
            .ThenInclude(tool => tool.HandoffOrchestration)
            .ThenInclude(orchestration => orchestration!.Routes)
            .ThenInclude(route => route.SourceAgentDefinition)
            .ThenInclude(agent => agent.Provider)
            .Include(definition => definition.Tools)
            .ThenInclude(tool => tool.ToolDefinition)
            .ThenInclude(tool => tool.HandoffOrchestration)
            .ThenInclude(orchestration => orchestration!.Routes)
            .ThenInclude(route => route.TargetAgentDefinition)
            .ThenInclude(agent => agent.Provider)
            .Include(definition => definition.McpAttachments)
            .ThenInclude(attachment => attachment.McpDefinition)
            .Include(definition => definition.Skills);

    public static async Task<Dictionary<int, AgentDefinition>> LoadRuntimeGraphAsync(
        SedgewrightDbContext dbContext,
        IEnumerable<int> agentIds,
        CancellationToken cancellationToken = default)
    {
        HashSet<int> ids = [.. agentIds];
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.AgentDefinitions
            .Where(definition => ids.Contains(definition.Id))
            .IncludeRuntimeGraph()
            .AsSplitQuery()
            .ToDictionaryAsync(definition => definition.Id, cancellationToken)
            .ConfigureAwait(false);
    }
}
