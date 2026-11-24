using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Discovers skill folders beneath configured and common source roots.
/// </summary>
public interface IAgentSkillDiscoveryService
{
    /// <summary>
    /// Gets existing per-user skill roots commonly used by agent tools.
    /// </summary>
    /// <returns>Existing common skill roots and their display names.</returns>
    IReadOnlyList<AgentSkillSource> GetCommonSkillSources();

    /// <summary>
    /// Finds skill folders beneath a source root, up to the configured maximum depth.
    /// </summary>
    /// <param name="sourceDirectory">The source root directory.</param>
    /// <returns>The discovered skill folders or a user-facing failure.</returns>
    Result<IReadOnlyList<DiscoveredAgentSkill>, IApplicationError> Discover(string sourceDirectory);
}
