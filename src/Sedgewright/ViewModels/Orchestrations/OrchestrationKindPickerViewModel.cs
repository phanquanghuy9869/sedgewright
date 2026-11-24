using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Sedgewright.Models;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.Orchestrations;

/// <summary>
/// Drives the orchestration kind picker dialog.
/// </summary>
public sealed class OrchestrationKindPickerViewModel : ViewModelBase
{
    private readonly RelayCommand _cancelCommand;

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event Action<OrchestrationKind?>? CloseRequested;

    /// <summary>
    /// Creates a new kind picker view-model.
    /// </summary>
    public OrchestrationKindPickerViewModel()
    {
        Options =
        [
            new OrchestrationKindPickerOptionViewModel(
                OrchestrationKind.Sequential,
                "Sequential",
                "Agents run one after another and each step feeds the next.",
                "Available",
                true,
                () => CloseRequested?.Invoke(OrchestrationKind.Sequential)),
            new OrchestrationKindPickerOptionViewModel(
                OrchestrationKind.Concurrent,
                "Concurrent",
                "Every agent sees the same prompt and responds in parallel.",
                "Available",
                true,
                () => CloseRequested?.Invoke(OrchestrationKind.Concurrent)),
            new OrchestrationKindPickerOptionViewModel(
                OrchestrationKind.GroupChat,
                "Group chat",
                "A manager coordinates shared conversation turns across the participant set.",
                "Available",
                true,
                () => CloseRequested?.Invoke(OrchestrationKind.GroupChat)),
            new OrchestrationKindPickerOptionViewModel(
                OrchestrationKind.Magentic,
                "Magentic",
                "A planning manager selects specialists dynamically and can pause for review.",
                "Available",
                true,
                () => CloseRequested?.Invoke(OrchestrationKind.Magentic)),
            new OrchestrationKindPickerOptionViewModel(
                OrchestrationKind.Handoff,
                "Handoff",
                "A start agent can transfer the conversation to other agents through routes.",
                "Available",
                true,
                () => CloseRequested?.Invoke(OrchestrationKind.Handoff)),
        ];

        _cancelCommand = new RelayCommand(Cancel);
    }

    /// <summary>
    /// Gets the picker title.
    /// </summary>
    public string WindowTitle => "Choose orchestration type";

    /// <summary>
    /// Gets the picker subtitle.
    /// </summary>
    public string SubtitleText => "Choose the orchestration pattern that matches the workflow you want to create.";

    /// <summary>
    /// Gets the available options.
    /// </summary>
    public ObservableCollection<OrchestrationKindPickerOptionViewModel> Options { get; }

    /// <summary>
    /// Gets the cancel command.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    private void Cancel()
        => CloseRequested?.Invoke(null);
}
