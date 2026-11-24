using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Runs a chat turn for the selected target.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Resolves the selected chat target and executes the corresponding chat pipeline.
    /// </summary>
    Task<UnitResult<IApplicationError>> SendAsync(
        IChatTarget selectedTarget,
        string prompt,
        Func<IntermediateUpdate, Task>? onIntermediateUpdate = null,
        Func<List<MessageModel>, Task>? onFinalOutput = null,
        Func<ToolApprovalRequestModel, Task<bool>>? onApprovalRequired = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the current session transcript for the selected agent target.
    /// </summary>
    Task<Result<List<MessageModel>, IApplicationError>> GetSelectedAgentSessionMessagesAsync(
        IChatTarget selectedTarget,
        CancellationToken cancellationToken = default);
}
