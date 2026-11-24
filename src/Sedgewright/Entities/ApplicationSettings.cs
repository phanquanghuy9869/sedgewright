using System;
using Sedgewright.Models;

namespace Sedgewright.Entities;

/// <summary>
/// Represents the singleton application settings row stored in SQLite.
/// </summary>
public sealed class ApplicationSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public string DefaultWorkingDirectory { get; set; } = string.Empty;

    public ThemeMode ThemeMode { get; set; } = ThemeMode.Light;

    public ChatTargetType? LastChatTargetType { get; set; }

    public int? LastChatTargetId { get; set; }

    public int? LastAgentSessionId { get; set; }
}
