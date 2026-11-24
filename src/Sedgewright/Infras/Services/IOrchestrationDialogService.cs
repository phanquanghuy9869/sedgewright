using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.ViewModels.Orchestrations;
using Sedgewright.ViewModels.Orchestrations.Concurrent;
using Sedgewright.ViewModels.Orchestrations.Sequential;
using Sedgewright.ViewModels.Orchestrations.GroupChat;
using Sedgewright.ViewModels.Orchestrations.Magentic;
using Sedgewright.ViewModels.Orchestrations.Handoff;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Opens orchestration editor dialogs.
/// </summary>
public interface IOrchestrationDialogService
{
    /// <summary>
    /// Shows the kind picker and returns the selected orchestration kind, or null when cancelled.
    /// </summary>
    Task<OrchestrationKind?> ShowKindPickerAsync(OrchestrationKindPickerViewModel viewModel);

    /// <summary>
    /// Shows the sequential orchestration editor and returns the save result.
    /// </summary>
    Task<UnitResult<IApplicationError>> ShowSequentialAsync(SequentialOrchestrationEditorViewModel viewModel);

    /// <summary>
    /// Shows the concurrent orchestration editor and returns the save result.
    /// </summary>
    Task<UnitResult<IApplicationError>> ShowConcurrentAsync(ConcurrentOrchestrationEditorViewModel viewModel);

    /// <summary>
    /// Shows the group chat orchestration editor and returns the save result.
    /// </summary>
    Task<UnitResult<IApplicationError>> ShowGroupChatAsync(GroupChatOrchestrationEditorViewModel viewModel);

    /// <summary>
    /// Shows the Magentic orchestration editor and returns the save result.
    /// </summary>
    Task<UnitResult<IApplicationError>> ShowMagenticAsync(MagenticOrchestrationEditorViewModel viewModel);

    /// <summary>
    /// Shows the handoff orchestration editor and returns the save result.
    /// </summary>
    Task<UnitResult<IApplicationError>> ShowHandoffAsync(HandoffOrchestrationEditorViewModel viewModel);
}
