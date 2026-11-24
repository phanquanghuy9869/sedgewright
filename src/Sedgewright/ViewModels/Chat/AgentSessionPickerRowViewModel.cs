using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Sedgewright.Models;

namespace Sedgewright.ViewModels.Chat;

/// <summary>
/// Represents one selectable session row in the agent session picker.
/// </summary>
public sealed class AgentSessionPickerRowViewModel
{
    /// <summary>
    /// Creates a new row model.
    /// </summary>
    public AgentSessionPickerRowViewModel(
        AgentSessionSnapshot session,
        bool canDelete,
        Func<AgentSessionSnapshot, Task> selectedCallback,
        Func<AgentSessionSnapshot, Task> deleteCallback)
    {
        Session = session;
        CanDelete = canDelete;
        SelectCommand = new AsyncRelayCommand(() => selectedCallback(session));
        DeleteCommand = new AsyncRelayCommand(() => deleteCallback(session), () => CanDelete);
    }

    /// <summary>
    /// Gets the underlying session snapshot.
    /// </summary>
    public AgentSessionSnapshot Session { get; }

    /// <summary>
    /// Gets the compact row text.
    /// </summary>
    public string DisplayText => $"{Session.DisplayLabel} · {Session.AgentModel} · {Session.AgentProviderDisplayName}";

    /// <summary>
    /// Gets whether the session can be deleted.
    /// </summary>
    public bool CanDelete { get; }

    /// <summary>
    /// Gets the selection command.
    /// </summary>
    public ICommand SelectCommand { get; }

    /// <summary>
    /// Gets the delete command.
    /// </summary>
    public ICommand DeleteCommand { get; }
}
