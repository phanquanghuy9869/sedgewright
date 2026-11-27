using System;

namespace Sedgewright.ViewModels.Skills;

/// <summary>
/// Represents one read-only skill discovered from a skill source.
/// </summary>
public sealed class SkillItemViewModel(
    string name,
    string directory,
    string relativePath)
{
    /// <summary>
    /// Gets the skill folder name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the absolute skill folder path.
    /// </summary>
    public string Directory { get; } = directory;

    /// <summary>
    /// Gets the path relative to the source root.
    /// </summary>
    public string RelativePath { get; } = relativePath;

    /// <summary>
    /// Gets the display name used in the skill list.
    /// </summary>
    public string DisplayName => string.Equals(
        RelativePath,
        Name,
        StringComparison.OrdinalIgnoreCase)
        ? Name
        : $@"{Name} ({RelativePath})";
}
