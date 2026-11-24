using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;

namespace Sedgewright.ViewModels.Agents;

/// <summary>
/// Holds provider and model selection state for the agent editor.
/// </summary>
public sealed class AgentDefinitionProviderModelViewModel : ViewModelBase
{
    private readonly Action _draftChanged;
    private readonly ProviderDiscoveryService _providerDiscoveryService = new();
    private readonly AsyncRelayCommand _chooseModelCommand;
    private LLMProvider? _selectedProvider;
    private string _model;
    private string _modelStatusText = string.Empty;

    /// <summary>
    /// Creates the provider and model editor state.
    /// </summary>
    /// <param name="draft">The initial draft values.</param>
    /// <param name="providers">The available providers.</param>
    /// <param name="draftChanged">Raised when the draft changes.</param>
    public AgentDefinitionProviderModelViewModel(
        AgentDefinitionEditorDraft draft,
        IReadOnlyList<LLMProvider> providers,
        Action draftChanged)
    {
        _draftChanged = draftChanged;
        Providers = providers;
        _model = draft.Model;
        _selectedProvider = ResolveSelectedProvider(draft.SelectedProvider, providers);
        _chooseModelCommand = new AsyncRelayCommand(ChooseModelAsync, CanChooseModel);
    }

    public event EventHandler<AgentModelSelectionRequestedEventArgs>? ModelSelectionRequested;

    public IReadOnlyList<LLMProvider> Providers { get; }

    public LoadingFeedbackViewModel LoadingFeedback { get; } = new();

    public LLMProvider? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (!SetProperty(ref _selectedProvider, value))
            {
                return;
            }

            _draftChanged();
            _chooseModelCommand.RaiseCanExecuteChanged();
        }
    }

    public string Model
    {
        get => _model;
        set
        {
            if (!SetProperty(ref _model, value))
            {
                return;
            }

            _draftChanged();
        }
    }

    public string ModelStatusText
    {
        get => _modelStatusText;
        private set
        {
            if (!SetProperty(ref _modelStatusText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasModelStatusText));
        }
    }

    public bool HasModelStatusText => !string.IsNullOrWhiteSpace(ModelStatusText);

    public ICommand ChooseModelCommand => _chooseModelCommand;

    internal void ClearModelStatus()
        => ModelStatusText = string.Empty;

    private bool CanChooseModel()
        => SelectedProvider is not null
            && ProviderDiscoveryService.CanDiscoverModels(SelectedProvider.ProviderKind.ToString())
            && (SelectedProvider.ProviderKind == LlmProviderKind.Copilot
                || !string.IsNullOrWhiteSpace(SelectedProvider.BaseUrl));

    private async Task ChooseModelAsync()
    {
        if (SelectedProvider is null || !CanChooseModel())
        {
            return;
        }

        LLMProvider selectedProvider = SelectedProvider;
        CancellationToken cancellationToken = LoadingFeedback.Begin("Discovering models...");
        try
        {
            IReadOnlyList<string> discoveredModels = await _providerDiscoveryService.DiscoverModelsAsync(
                selectedProvider.ProviderKind.ToString(),
                selectedProvider.BaseUrl,
                selectedProvider.ApiKey,
                cancellationToken);

            if (discoveredModels.Count == 0)
            {
                ModelStatusText = "No models were returned by the selected provider.";
                return;
            }

            ModelSelectionRequested?.Invoke(
                this,
                new AgentModelSelectionRequestedEventArgs(
                    selectedProvider.DisplayName,
                    discoveredModels,
                    string.IsNullOrWhiteSpace(Model) ? [] : [Model]));

            ModelStatusText = string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ModelStatusText = "Model discovery canceled.";
        }
        catch (Exception ex)
        {
            ModelStatusText = $"Couldn't load models from the selected provider: {ex.Message}";
        }
        finally
        {
            LoadingFeedback.Complete();
        }
    }

    private static LLMProvider? ResolveSelectedProvider(LLMProvider? selectedProvider, IReadOnlyList<LLMProvider> providers)
    {
        if (providers.Count == 0)
        {
            return null;
        }

        if (selectedProvider is null)
        {
            return providers[0];
        }

        return providers.FirstOrDefault(provider => provider.Id == selectedProvider.Id) ?? providers[0];
    }
}
