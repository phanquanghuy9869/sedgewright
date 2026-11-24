using System.Collections.Generic;
using Microsoft.Agents.AI;
using Sedgewright.Entities;

namespace Sedgewright.Infras.MAF.Extensions;

public static class AIContextProviderExtensions
{
    extension(AIContextProvider provider)
    {
        public static IEnumerable<AIContextProvider>? From(AgentDefinition agentDefinition)
        {
            AgentSkillsProvider? skillsProvider = AgentSkillsProvider.From(agentDefinition.Skills);

            return skillsProvider is null ? null : new[] { skillsProvider };
        }
    }
}
