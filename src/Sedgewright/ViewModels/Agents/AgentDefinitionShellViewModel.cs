using System;
using System.Collections.Generic;
using System.Windows.Input;
using Sedgewright.Entities;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Holds shell execution options for the agent editor.
/// </summary>
public sealed class AgentDefinitionShellViewModel : ViewModelBase
{
    private readonly Action _draftChanged;
    private readonly Action<string> _reportStatus;
    private readonly RelayCommand _browseShellWorkingDirectoryCommand;
    private ShellExecutorType _shellExecutor;
    private string _shellWorkingDirectory;
    private bool _isShellActive;

    /// <summary>
    /// Creates the shell options editor state.
    /// </summary>
    /// <param name="draft">The initial draft values.</param>
    /// <param name="draftChanged">Raised when the draft changes.</param>
    /// <param name="reportStatus">Reports nonfatal workflow status back to the editor.</param>
    public AgentDefinitionShellViewModel(
        AgentDefinitionEditorDraft draft,
        Action draftChanged,
        Action<string> reportStatus)
    {
        _draftChanged = draftChanged;
        _reportStatus = reportStatus;
        _shellExecutor = draft.ShellExecutor;
        _shellWorkingDirectory = draft.ShellWorkingDirectory;
        _isShellActive = draft.IsShellActive;
        _browseShellWorkingDirectoryCommand = new RelayCommand(
            RequestShellWorkingDirectorySelection);
    }

    public event EventHandler? ShellWorkingDirectorySelectionRequested;

    public IReadOnlyList<ShellExecutorType> ShellExecutors { get; } = Enum.GetValues<ShellExecutorType>();

    public ShellExecutorType ShellExecutor
    {
        get => _shellExecutor;
        set
        {
            if (!SetProperty(ref _shellExecutor, value))
            {
                return;
            }

            _draftChanged();
        }
    }

    public string ShellWorkingDirectory
    {
        get => _shellWorkingDirectory;
        set
        {
            if (!SetProperty(ref _shellWorkingDirectory, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShellSummary));
            _draftChanged();
        }
    }

    public bool IsShellActive
    {
        get => _isShellActive;
        set
        {
            if (!SetProperty(ref _isShellActive, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShellSummary));
            _draftChanged();
        }
    }

    public string ShellSummary
        => !IsShellActive
            ? "Shell disabled"
            : string.IsNullOrWhiteSpace(ShellWorkingDirectory)
                ? "Shell enabled"
                : $"Shell enabled · {ShellWorkingDirectory}";

    public ICommand BrowseShellWorkingDirectoryCommand => _browseShellWorkingDirectoryCommand;

    internal void ApplyShellWorkingDirectory(string directory)
    {
        string normalizedDirectory = Normalize(directory);
        if (normalizedDirectory.Length == 0)
        {
            ShowStatus("A shell working directory is required.");
            return;
        }

        ShellWorkingDirectory = normalizedDirectory;
    }

    internal void ShowStatus(string status)
        => _reportStatus(status);

    private void RequestShellWorkingDirectorySelection()
        => ShellWorkingDirectorySelectionRequested?.Invoke(this, EventArgs.Empty);

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
