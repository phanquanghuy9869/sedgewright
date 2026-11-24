using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Models;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Discovers skill folders recursively using the SKILL.md marker.
/// </summary>
public sealed class AgentSkillDiscoveryService : IAgentSkillDiscoveryService
{
    /// <summary>
    /// Gets the maximum number of directory levels searched beneath each source root.
    /// </summary>
    public const int MaximumSkillSearchDepth = 8;

    private readonly string _userProfileDirectory;

    /// <summary>
    /// Creates the skill discovery service.
    /// </summary>
    /// <param name="userProfileDirectory">The profile directory used to resolve common skill roots.</param>
    public AgentSkillDiscoveryService(string? userProfileDirectory = null)
    {
        _userProfileDirectory = string.IsNullOrWhiteSpace(userProfileDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : userProfileDirectory.Trim();
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentSkillSource> GetCommonSkillSources()
    {
        if (string.IsNullOrWhiteSpace(_userProfileDirectory))
        {
            return [];
        }

        AgentSkillSource[] commonSources =
        [
            new(".agents", Path.Combine(_userProfileDirectory, ".agents", "skills")),
            new(".copilot", Path.Combine(_userProfileDirectory, ".copilot", "skills")),
            new(".claude", Path.Combine(_userProfileDirectory, ".claude", "skills")),
            new(".codex", Path.Combine(_userProfileDirectory, ".codex", "skills")),
            new("OpenCode", Path.Combine(_userProfileDirectory, ".config", "opencode", "skills")),
        ];

        return commonSources
            .Where(source => Directory.Exists(source.Directory))
            .ToArray();
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<DiscoveredAgentSkill>, IApplicationError> Discover(string sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return new ValidationError(["A skills directory is required."]);
        }

        string normalizedDirectory = Path.TrimEndingDirectorySeparator(sourceDirectory.Trim());
        if (!Directory.Exists(normalizedDirectory))
        {
            return new ValidationError([$"The skills directory does not exist: {normalizedDirectory}"]);
        }

        try
        {
            List<DiscoveredAgentSkill> discoveredSkills = [];
            DiscoverFromDirectory(normalizedDirectory, normalizedDirectory, currentDepth: 0, discoveredSkills);

            IReadOnlyList<DiscoveredAgentSkill> orderedSkills = discoveredSkills
                .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(skill => skill.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Result.Success<IReadOnlyList<DiscoveredAgentSkill>, IApplicationError>(orderedSkills);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException)
        {
            return new GenericError([$"Could not scan the skills directory: {exception.Message}"]);
        }
    }

    private static void DiscoverFromDirectory(
        string sourceDirectory,
        string currentDirectory,
        int currentDepth,
        ICollection<DiscoveredAgentSkill> discoveredSkills)
    {
        if (currentDepth == MaximumSkillSearchDepth)
        {
            return;
        }

        foreach (string childDirectory in Directory.EnumerateDirectories(currentDirectory))
        {
            FileAttributes attributes = File.GetAttributes(childDirectory);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            int childDepth = currentDepth + 1;
            if (ContainsSkillFile(childDirectory))
            {
                string? name = Path.GetFileName(childDirectory);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    discoveredSkills.Add(new DiscoveredAgentSkill(
                        name,
                        childDirectory,
                        Path.GetRelativePath(sourceDirectory, childDirectory)));
                }
            }

            DiscoverFromDirectory(sourceDirectory, childDirectory, childDepth, discoveredSkills);
        }
    }

    private static bool ContainsSkillFile(string directory)
        => Directory.EnumerateFiles(directory)
            .Any(file => string.Equals(Path.GetFileName(file), "SKILL.md", StringComparison.OrdinalIgnoreCase));
}
