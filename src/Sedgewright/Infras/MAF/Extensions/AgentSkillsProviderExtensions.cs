using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.MAF.Extensions;

public static class AgentSkillsProviderExtensions
{
    extension(AgentSkillsProvider)
    {
        public static AgentSkillsProvider? From(IEnumerable<AgentSkillDefinition> skillDefinitions)
        {
            if (skillDefinitions is null || !skillDefinitions.Any())
            {
                return null;
            }
            IEnumerable<AgentSkillsSource> skillsSources = skillDefinitions.Select(AgentSkillsSource.From);
            AgentSkillsProviderBuilder providerBuilder = new();
            foreach (AgentSkillsSource skillsSource in skillsSources)
            {
                providerBuilder.UseSource(skillsSource);
            }
            return providerBuilder.Build();
        }
    }

    extension(AgentSkillsSource)
    {
        public static AgentSkillsSource From(AgentSkillDefinition skillDefinition)
        {
            AgentFileSkillsSource source = new(
                skillPath: skillDefinition.SkillDirectory,
                scriptRunner: SubprocessScriptRunner.RunAsync
            );

            if (skillDefinition.HasNoSkills)
            {
                return source;
            }

            FilteringAgentSkillsSource filteredSource = new(
                source,
                (skill, _) => skillDefinition.SelectedSkills.Contains(skill.Frontmatter.Name, StringComparer.OrdinalIgnoreCase)
            );
            return filteredSource;
        }
    }
}
