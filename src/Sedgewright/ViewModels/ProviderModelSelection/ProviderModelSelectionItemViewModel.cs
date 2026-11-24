using System;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.ProviderModelSelection;

/// <summary>
/// Represents a selectable model entry inside the chooser window.
/// </summary>
/// <remarks>
/// Creates a new selectable model entry.
/// </remarks>
/// <param name="modelId">The model id.</param>
/// <param name="isEnabled">Whether the model starts selected.</param>
/// <param name="selectionChanged">Callback for selection changes.</param>
public sealed class ProviderModelSelectionItemViewModel(string modelId, bool isEnabled, Action selectionChanged) : ViewModelBase
{

    /// <summary>
    /// Gets the model id.
    /// </summary>
    public string ModelId { get; } = modelId;

    /// <summary>
    /// Gets or sets whether the model is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (!SetProperty(ref isEnabled, value))
            {
                return;
            }

            selectionChanged();
        }
    }
}
