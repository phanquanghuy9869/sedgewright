using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;
using Microsoft.Agents.AI;
using Sedgewright.ViewModels.Chat;
using Sedgewright.Views.Chat;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Opens the modal chat target picker dialog.
/// </summary>
public sealed class ChatTargetDialogService(
    IChatTargetCatalogService catalogService,
    ISharedState sharedState,
    IAgentDefinitionRepository agentDefinitionRepository,
    IAgentSessionRepository agentSessionRepository,
    IChatSessionService chatSessionService) : IChatTargetDialogService
{
    private readonly IChatTargetCatalogService _catalogService = catalogService;
    private readonly ISharedState _sharedState = sharedState;
    private readonly IAgentDefinitionRepository _agentDefinitionRepository = agentDefinitionRepository;
    private readonly IAgentSessionRepository _agentSessionRepository = agentSessionRepository;
    private readonly IChatSessionService _chatSessionService = chatSessionService;

    /// <inheritdoc />
    public async Task<IChatTarget?> ShowAsync(IChatTarget? selectedTarget = null)
    {
        Window owner = GetOwnerWindow();

        ChatTargetPickerViewModel viewModel = new(_catalogService, selectedTarget);
        await viewModel.LoadAsync();

        ChatTargetPickerWindow window = new()
        {
            DataContext = viewModel,
        };

        IChatTarget? chosenTarget = await window.ShowDialog<IChatTarget?>(owner);
        if (chosenTarget is null)
        {
            return null;
        }

        if (chosenTarget is AgentDefinition selectedAgent)
        {
            AgentDefinition? agentDefinition = await _agentDefinitionRepository
                .GetAsync(selectedAgent.Id);
            if (agentDefinition is null)
            {
                return null;
            }

            AgentSessionPickerViewModel sessionViewModel = new(
                _sharedState,
                agentDefinition,
                _agentSessionRepository,
                _chatSessionService);
            await sessionViewModel.LoadAsync();

            AgentSessionPickerWindow sessionWindow = new()
            {
                DataContext = sessionViewModel,
            };

            AgentRuntime? selectedSession = await sessionWindow.ShowDialog<AgentRuntime?>(owner);
            if (selectedSession is null)
            {
                return null;
            }
        }

        return chosenTarget;
    }

    private static Window GetOwnerWindow()
    {
        IClassicDesktopStyleApplicationLifetime? desktopLifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return desktopLifetime?.MainWindow ?? throw new InvalidOperationException("Main window is not available.");
    }
}
