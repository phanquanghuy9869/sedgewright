using System;
using System.Windows.Input;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Holds Harness-specific options for the agent editor.
/// </summary>
public sealed class AgentDefinitionHarnessOptionsViewModel : ViewModelBase
{
    private readonly Action _draftChanged;
    private readonly RelayCommand _openHarnessOptionsCommand;
    private bool _suppressDraftChanged;
    private bool _isHarnessAgent;
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
    /// Creates the Harness options editor state.
    /// </summary>
    /// <param name="draft">The initial draft values.</param>
    /// <param name="canToggleHarnessAgentType">Whether the Harness toggle can be changed.</param>
    /// <param name="draftChanged">Raised when the draft changes.</param>
    public AgentDefinitionHarnessOptionsViewModel(
        AgentDefinitionEditorDraft draft,
        bool canToggleHarnessAgentType,
        Action draftChanged)
    {
        _draftChanged = draftChanged;
        _openHarnessOptionsCommand = new RelayCommand(OpenHarnessOptions);
        CanToggleHarnessAgentType = canToggleHarnessAgentType;
        _isHarnessAgent = draft.IsHarnessAgent;
        _disableTodoProvider = draft.DisableTodoProvider;
        _disableAgentModeProvider = draft.DisableAgentModeProvider;
        _disableFileMemory = draft.DisableFileMemory;
        _disableWebSearch = draft.DisableWebSearch;
        _disableToolAutoApproval = draft.DisableToolAutoApproval;
        _disableAgentSkillsProvider = draft.DisableAgentSkillsProvider;
        _disableOpenTelemetry = draft.DisableOpenTelemetry;
        _maxContextWindowTokens = draft.MaxContextWindowTokens;
        _maxOutputTokens = draft.MaxOutputTokens;
        _enableFileAccess = draft.EnableFileAccess;
        _enableCodeExecution = draft.EnableCodeExecution;
    }

    public event EventHandler<AgentHarnessOptionsSelectionRequestedEventArgs>? HarnessOptionsSelectionRequested;

    public bool CanToggleHarnessAgentType { get; }

    public ICommand OpenHarnessOptionsCommand => _openHarnessOptionsCommand;

    public string HarnessSummary
    {
        get
        {
            if (!IsHarnessAgent)
            {
                return "Harness agent disabled";
            }

            int configuredOptionCount = 0;
            configuredOptionCount += DisableTodoProvider ? 1 : 0;
            configuredOptionCount += DisableAgentModeProvider ? 1 : 0;
            configuredOptionCount += DisableFileMemory ? 1 : 0;
            configuredOptionCount += DisableWebSearch ? 1 : 0;
            configuredOptionCount += DisableToolAutoApproval ? 1 : 0;
            configuredOptionCount += DisableAgentSkillsProvider ? 1 : 0;
            configuredOptionCount += DisableOpenTelemetry ? 1 : 0;
            configuredOptionCount += EnableCodeExecution ? 1 : 0;

            return configuredOptionCount == 0
                ? "Harness agent enabled"
                : $"Harness agent enabled; {configuredOptionCount} options configured";
        }
    }

    public AgentHarnessOptionsSelection Selection => new(
        IsHarnessAgent,
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

    public bool IsHarnessAgent
    {
        get => _isHarnessAgent;
        set
        {
            if (!CanToggleHarnessAgentType)
            {
                return;
            }

            AgentHarnessOptionsSelection previousSelection = Selection;
            if (!SetProperty(ref _isHarnessAgent, value))
            {
                return;
            }

            OnOptionChanged();
            if (IsHarnessAgent)
            {
                RequestHarnessOptionsSelection(previousSelection);
            }
        }
    }

    public bool DisableTodoProvider
    {
        get => _disableTodoProvider;
        set
        {
            if (!SetProperty(ref _disableTodoProvider, value))
            {
                return;
            }

            OnOptionChanged();
        }
    }

    public bool DisableAgentModeProvider
    {
        get => _disableAgentModeProvider;
        set
        {
            if (!SetProperty(ref _disableAgentModeProvider, value))
            {
                return;
            }

            OnOptionChanged();
        }
    }

    public bool DisableFileMemory
    {
        get => _disableFileMemory;
        set
        {
            if (!SetProperty(ref _disableFileMemory, value))
            {
                return;
            }

            OnOptionChanged();
        }
    }

    public bool DisableWebSearch
    {
        get => _disableWebSearch;
        set
        {
            if (!SetProperty(ref _disableWebSearch, value))
            {
                return;
            }

            OnOptionChanged();
        }
    }

    public bool DisableToolAutoApproval
    {
        get => _disableToolAutoApproval;
        set
        {
            if (!SetProperty(ref _disableToolAutoApproval, value))
            {
                return;
            }

            OnOptionChanged();
        }
    }

    public bool DisableAgentSkillsProvider
    {
        get => _disableAgentSkillsProvider;
        set
        {
            if (!SetProperty(ref _disableAgentSkillsProvider, value))
            {
                return;
            }

            OnOptionChanged();
        }
    }

    public bool DisableOpenTelemetry
    {
        get => _disableOpenTelemetry;
        set
        {
            if (!SetProperty(ref _disableOpenTelemetry, value))
            {
                return;
            }

            OnOptionChanged();
        }
    }

    public int? MaxContextWindowTokens
    {
        get => _maxContextWindowTokens;
        set
        {
            if (!SetProperty(ref _maxContextWindowTokens, value))
            {
                return;
            }

            OnOptionChanged();
        }
    }

    public int? MaxOutputTokens
    {
        get => _maxOutputTokens;
        set
        {
            if (!SetProperty(ref _maxOutputTokens, value))
            {
                return;
            }

            OnOptionChanged();
        }
    }

    public bool EnableFileAccess
    {
        get => _enableFileAccess;
        set
        {
            if (!SetProperty(ref _enableFileAccess, value))
            {
                return;
            }

            OnOptionChanged();
        }
    }

    public bool EnableCodeExecution
    {
        get => _enableCodeExecution;
        set
        {
            if (!SetProperty(ref _enableCodeExecution, value))
            {
                return;
            }

            OnOptionChanged();
        }
    }

    internal void ApplyHarnessOptionsSelection(AgentHarnessOptionsSelection selection)
    {
        AgentHarnessOptionsSelection effectiveSelection = CanToggleHarnessAgentType
            ? selection
            : selection with { IsHarnessAgent = IsHarnessAgent };

        if (Selection == effectiveSelection)
        {
            return;
        }

        _suppressDraftChanged = true;
        try
        {
            IsHarnessAgent = effectiveSelection.IsHarnessAgent;
            DisableTodoProvider = effectiveSelection.DisableTodoProvider;
            DisableAgentModeProvider = effectiveSelection.DisableAgentModeProvider;
            DisableFileMemory = effectiveSelection.DisableFileMemory;
            DisableWebSearch = effectiveSelection.DisableWebSearch;
            DisableToolAutoApproval = effectiveSelection.DisableToolAutoApproval;
            DisableAgentSkillsProvider = effectiveSelection.DisableAgentSkillsProvider;
            DisableOpenTelemetry = effectiveSelection.DisableOpenTelemetry;
            MaxContextWindowTokens = effectiveSelection.MaxContextWindowTokens;
            MaxOutputTokens = effectiveSelection.MaxOutputTokens;
            EnableFileAccess = effectiveSelection.EnableFileAccess;
            EnableCodeExecution = effectiveSelection.EnableCodeExecution;
        }
        finally
        {
            _suppressDraftChanged = false;
        }

        _draftChanged();
    }

    private void OpenHarnessOptions()
        => RequestHarnessOptionsSelection(null);

    private void RequestHarnessOptionsSelection(AgentHarnessOptionsSelection? revertSelection)
        => HarnessOptionsSelectionRequested?.Invoke(
            this,
            new AgentHarnessOptionsSelectionRequestedEventArgs(Selection, revertSelection));

    private void OnOptionChanged()
    {
        OnPropertyChanged(nameof(HarnessSummary));
        if (!_suppressDraftChanged)
        {
            _draftChanged();
        }
    }
}
