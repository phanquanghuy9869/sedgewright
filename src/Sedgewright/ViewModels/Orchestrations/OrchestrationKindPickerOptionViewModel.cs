using System;
using System.Windows.Input;
using Sedgewright.Models;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.Orchestrations;

/// <summary>
/// Represents one selectable orchestration kind in the picker.
/// </summary>
public sealed class OrchestrationKindPickerOptionViewModel
{
    /// <summary>
    /// Creates a picker option.
    /// </summary>
    public OrchestrationKindPickerOptionViewModel(
        OrchestrationKind kind,
        string title,
        string description,
        string availabilityText,
        bool isEnabled,
        Action select)
    {
        Kind = kind;
        Title = title;
        Description = description;
        AvailabilityText = availabilityText;
        IsEnabled = isEnabled;
        SelectCommand = new RelayCommand(select, () => IsEnabled);
    }

    /// <summary>
    /// Gets the orchestration kind.
    /// </summary>
    public OrchestrationKind Kind { get; }

    /// <summary>
    /// Gets the title text.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the description text.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the availability label.
    /// </summary>
    public string AvailabilityText { get; }

    /// <summary>
    /// Gets whether the option is enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets the select command.
    /// </summary>
    public ICommand SelectCommand { get; }
}
