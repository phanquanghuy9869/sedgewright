namespace Sedgewright.Models;

/// <summary>
/// Describes a root directory that contributes skills to the agent picker.
/// </summary>
/// <param name="Name">The display name shown for this source group.</param>
/// <param name="Directory">The root directory to scan for skills.</param>
public sealed record AgentSkillSource(string Name, string Directory);

/// <summary>
/// Describes a skill folder discovered beneath a source root.
/// </summary>
/// <param name="Name">The skill folder name.</param>
/// <param name="Directory">The absolute path to the skill folder.</param>
/// <param name="RelativePath">The path from the source root to the skill folder.</param>
public sealed record DiscoveredAgentSkill(string Name, string Directory, string RelativePath);
