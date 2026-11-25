using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.ViewModels.Chat;

namespace Sedgewright.ViewModels.ScheduledJobs;

/// <summary>
/// Represents one selectable agent or orchestration target.
/// </summary>
public sealed class ScheduledJobTargetOptionViewModel(IChatTarget target) : ViewModelBase
{
    /// <summary>
    /// Gets the raw target.
    /// </summary>
    public IChatTarget Target { get; } = target;

    /// <summary>
    /// Gets the stable target identity.
    /// </summary>
    public ChatTargetIdentity Identity { get; } = ChatTargetIdentity.From(target);

    /// <summary>
    /// Gets the display label.
    /// </summary>
    public string DisplayLabel { get; } = ChatTargetDisplayInfo.From(target).Summary;
}
