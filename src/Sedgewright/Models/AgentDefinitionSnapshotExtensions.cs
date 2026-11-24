using Sedgewright.Entities;

namespace Sedgewright.Models;

/// <summary>
/// Converts agent definitions into list-friendly snapshots.
/// </summary>
public static class AgentDefinitionSnapshotExtensions
{
    extension(AgentDefinitionSnapshot)
    {
        /// <summary>
        /// Creates a lightweight snapshot for list rendering.
        /// </summary>
        public static AgentDefinitionSnapshot From(AgentDefinition agentDefinition, int usageCount)
            => new(
                agentDefinition.Id,
                agentDefinition.Name,
                agentDefinition.Description,
                agentDefinition.IsReadOnly,
                agentDefinition.Model,
                agentDefinition.Provider.DisplayName,
                agentDefinition.McpAttachments.Count,
                usageCount);
    }
}
