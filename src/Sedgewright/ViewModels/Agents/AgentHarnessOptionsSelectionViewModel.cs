using System;
using System.Windows.Input;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Drives the modal Harness options selection dialog.
/// </summary>
public sealed class AgentHarnessOptionsSelectionViewModel : ViewModelBase
{
    private readonly bool _isHarnessAgent;
    private readonly RelayCommand _applyCommand;
    private readonly RelayCommand _cancelCommand;
    private bool _disableTodoProvider;
    private bool _disableAgentModeProvider;
    private bool _disableFileMemory;
    private bool _disableWebSearch;
    private bool _disableToolAutoApproval;
    private bool _disableAgentSkillsProvider;
    private bool _disableOpenTelemetry;
    private int? _maxContextWindowTokens;
    private int? _maxOutputTokens;
    private bool _enableFileAccess;
    private bool _enableCodeExecution;

    /// <summary>
    /// Creates the Harness options selection dialog state.
    /// </summary>
    /// <param name="initialSelection">The settings selected before opening the dialog.</param>
    /// <param name="canToggleHarnessAgentType">Whether the Harness agent type can be changed.</param>
    public AgentHarnessOptionsSelectionViewModel(
        AgentHarnessOptionsSelection initialSelection)
    {
        _isHarnessAgent = initialSelection.IsHarnessAgent;
        _disableTodoProvider = initialSelection.DisableTodoProvider;
        _disableAgentModeProvider = initialSelection.DisableAgentModeProvider;
        _disableFileMemory = initialSelection.DisableFileMemory;
        _disableWebSearch = initialSelection.DisableWebSearch;
        _disableToolAutoApproval = initialSelection.DisableToolAutoApproval;
        _disableAgentSkillsProvider = initialSelection.DisableAgentSkillsProvider;
        _disableOpenTelemetry = initialSelection.DisableOpenTelemetry;
        _maxContextWindowTokens = initialSelection.MaxContextWindowTokens;
        _maxOutputTokens = initialSelection.MaxOutputTokens;
        _enableFileAccess = initialSelection.EnableFileAccess;
        _enableCodeExecution = initialSelection.EnableCodeExecution;

        _applyCommand = new RelayCommand(ApplySelection);
        _cancelCommand = new RelayCommand(CancelSelection);
    }

    /// <summary>
    /// Raised when the dialog requests closing.
    /// </summary>
    public event EventHandler<AgentHarnessOptionsSelectionCloseRequestedEventArgs>? CloseRequested;

    public bool DisableTodoProvider
    {
        get => _disableTodoProvider;
        set => SetProperty(ref _disableTodoProvider, value);
    }

    public bool DisableAgentModeProvider
    {
        get => _disableAgentModeProvider;
        set => SetProperty(ref _disableAgentModeProvider, value);
    }

    public bool DisableFileMemory
    {
        get => _disableFileMemory;
        set => SetProperty(ref _disableFileMemory, value);
    }

    public bool DisableWebSearch
    {
        get => _disableWebSearch;
        set => SetProperty(ref _disableWebSearch, value);
    }

    public bool DisableToolAutoApproval
    {
        get => _disableToolAutoApproval;
        set => SetProperty(ref _disableToolAutoApproval, value);
    }

    public bool DisableAgentSkillsProvider
    {
        get => _disableAgentSkillsProvider;
        set => SetProperty(ref _disableAgentSkillsProvider, value);
    }

    public bool DisableOpenTelemetry
    {
        get => _disableOpenTelemetry;
        set => SetProperty(ref _disableOpenTelemetry, value);
    }

    public int? MaxContextWindowTokens
    {
        get => _maxContextWindowTokens;
        set => SetProperty(ref _maxContextWindowTokens, value);
    }

    public int? MaxOutputTokens
    {
        get => _maxOutputTokens;
        set => SetProperty(ref _maxOutputTokens, value);
    }

    public bool EnableFileAccess
    {
        get => _enableFileAccess;
        set => SetProperty(ref _enableFileAccess, value);
    }

    public bool EnableCodeExecution
    {
        get => _enableCodeExecution;
        set => SetProperty(ref _enableCodeExecution, value);
    }

    public ICommand ApplyCommand => _applyCommand;

    public ICommand CancelCommand => _cancelCommand;

    public string WindowTitle => "Harness options";

    public AgentHarnessOptionsSelection Selection => new(
        _isHarnessAgent,
        DisableTodoProvider,
        DisableAgentModeProvider,
        DisableFileMemory,
        DisableWebSearch,
        DisableToolAutoApproval,
        DisableAgentSkillsProvider,
        DisableOpenTelemetry,
        MaxContextWindowTokens,
        MaxOutputTokens,
        EnableFileAccess,
        EnableCodeExecution);

    private void ApplySelection()
        => CloseRequested?.Invoke(this, new AgentHarnessOptionsSelectionCloseRequestedEventArgs(true));

    private void CancelSelection()
        => CloseRequested?.Invoke(this, new AgentHarnessOptionsSelectionCloseRequestedEventArgs(false));
}
