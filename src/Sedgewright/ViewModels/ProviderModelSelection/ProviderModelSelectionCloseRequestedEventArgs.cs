using System;

namespace Sedgewright.ViewModels.ProviderModelSelection;

/// <summary>
/// Requests that the chooser window closes.
/// </summary>
/// <remarks>
/// Creates a new close request.
/// </remarks>
/// <param name="accepted">Whether the current selection should be accepted.</param>
public sealed class ProviderModelSelectionCloseRequestedEventArgs(bool accepted) : EventArgs
{

    /// <summary>
    /// Gets whether the selection should be accepted.
    /// </summary>
    public bool Accepted { get; } = accepted;
}
