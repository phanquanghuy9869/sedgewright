namespace Sedgewright.ViewModels.Chat;

/// <summary>
/// Represents the label and subtitle shown for a chat target.
/// </summary>
public sealed record ChatTargetDisplayInfo(string Label, string Subtitle)
{
    /// <summary>
    /// Gets a compact combined summary of the label and subtitle.
    /// </summary>
    public string Summary => string.IsNullOrWhiteSpace(Subtitle) ? Label : $"{Label} · {Subtitle}";
}