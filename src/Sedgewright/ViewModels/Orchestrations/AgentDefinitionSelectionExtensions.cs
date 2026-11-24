using System.Collections.Generic;
using System.Linq;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Orchestrations;

/// <summary>
/// Provides shared agent-selection helpers for orchestration editors.
/// </summary>
public static class AgentDefinitionSelectionExtensions
{
    extension(IReadOnlyList<AgentDefinition> availableAgents)
    {
        /// <summary>
        /// Resolves a selected agent against the shared agent list by identifier.
        /// </summary>
        public AgentDefinition? ResolveSelectedAgent(AgentDefinition? selectedAgent)
        {
            if (selectedAgent is null)
            {
                return availableAgents.FirstOrDefault();
            }

            return availableAgents.FirstOrDefault(agent => agent.Id == selectedAgent.Id)
                ?? selectedAgent;
        }
    }
}
