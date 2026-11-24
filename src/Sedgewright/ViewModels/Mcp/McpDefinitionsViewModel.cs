using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.Services;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Mcp;

/// <summary>
/// Manages the MCP list page.
/// </summary>
public sealed class McpDefinitionsViewModel : ViewModelBase
{
    private readonly IMcpDefinitionRepository _repository;
    private readonly IMcpDefinitionDialogService _dialogService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _addCommand;
    private string _statusText = string.Empty;

    /// <summary>
    /// Creates the MCP list view-model.
    /// </summary>
    public McpDefinitionsViewModel(
        IMcpDefinitionRepository repository,
        IMcpDefinitionDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
        Items = [];

        _refreshCommand = new AsyncRelayCommand(RefreshAsync);
        _addCommand = new AsyncRelayCommand(AddAsync);

        _refreshCommand.Execute(null);
    }

    public ObservableCollection<McpDefinitionListItemViewModel> Items { get; }

    public bool HasItems => Items.Count > 0;

    public bool HasNoItems => !HasItems;

    public string TitleText => "MCP definitions";

    public string SubtitleText => "Manage local MCP servers with a compact form or a raw Json config.";

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand AddCommand => _addCommand;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (!SetProperty(ref _statusText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasStatusText));
        }
    }

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    public string EmptyStateTitle => "No MCP definitions yet.";

    public string EmptyStateText => "Add a definition, switch to Json when needed, and keep the layout minimal.";

    private async Task RefreshAsync()
    {
        try
        {
            System.Collections.Generic.List<McpDefinitionSnapshot> snapshots = await _repository.GetAllAsync();
            Items.Clear();

            foreach (McpDefinitionSnapshot snapshot in snapshots)
            {
                Items.Add(CreateRow(snapshot));
            }

            StatusText = string.Empty;
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasNoItems));
        }
        catch (Exception exception)
        {
            StatusText = $"Could not load MCP definitions: {exception.Message}";
        }
    }

    private async Task AddAsync()
    {
        await EditAsync(null);
    }

    private async Task EditAsync(int? id)
    {
        McpDefinition? definition = id.HasValue
            ? await _repository.GetAsync(id.Value)
            : null;

        if (id.HasValue && definition is null)
        {
            StatusText = "The selected MCP definition no longer exists.";
            return;
        }

        McpDefinitionEditorViewModel editor = new(
            definition,
            _repository,
            (name, excludeId) => _repository.NameExistsAsync(name, excludeId));

        UnitResult<IApplicationError> saveResult = await _dialogService.ShowAsync(editor);
        if (saveResult.IsFailure)
        {
            return;
        }

        await RefreshAsync();
    }

    private async Task DeleteAsync(int id)
    {
        if (await _repository.HasAttachmentsAsync(id))
        {
            StatusText = "This MCP definition is attached to one or more agents and cannot be deleted.";
            return;
        }

        McpDefinition? definition = await _repository.GetAsync(id);
        if (definition is null)
        {
            StatusText = "The selected MCP definition no longer exists.";
            return;
        }

        try
        {
            _repository.Delete(definition);
            await _repository.SaveChangesAsync();
            await RefreshAsync();
        }
        catch (DbUpdateException exception)
        {
            StatusText = $"Could not delete the MCP definition: {exception.Message}";
        }
    }

    private McpDefinitionListItemViewModel CreateRow(McpDefinitionSnapshot snapshot)
        => new(
            snapshot,
            () => EditAsync(snapshot.Id),
            () => DeleteAsync(snapshot.Id));
}
