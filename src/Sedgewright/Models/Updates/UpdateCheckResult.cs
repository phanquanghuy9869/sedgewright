namespace Sedgewright.Models.Updates;

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string? AvailableVersion,
    string? ErrorMessage = null);
