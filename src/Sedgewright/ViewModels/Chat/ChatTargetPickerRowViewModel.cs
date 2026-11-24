using System;
using System.Windows.Input;
using Sedgewright.Entities;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.Chat;

/// <summary>
/// Represents one selectable row in the chat target picker.
/// </summary>
public sealed class ChatTargetPickerRowViewModel
{
    /// <summary>
    /// Creates a new row model.
    /// </summary>
    public ChatTargetPickerRowViewModel(IChatTarget target, string title, string subtitle, Action<IChatTarget> selectedCallback)
    {
        Target = target;
        Title = title;
        Subtitle = subtitle;
        SelectCommand = new RelayCommand(() => selectedCallback(target));
    }

    /// <summary>
    /// Gets the raw chat target.
    /// </summary>
    public IChatTarget Target { get; }

    /// <summary>
    /// Gets the row title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the row subtitle.
    /// </summary>
    public string Subtitle { get; }

    /// <summary>
    /// Gets the selection command.
    /// </summary>
    public ICommand SelectCommand { get; }
}
