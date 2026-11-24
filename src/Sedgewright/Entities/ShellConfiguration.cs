namespace Sedgewright.Entities;

/// <summary>
/// Describes how an agent may execute shell commands.
/// </summary>
public record ShellConfiguration(ShellExecutorType Executor, string WorkingDirectory, bool IsActive);
