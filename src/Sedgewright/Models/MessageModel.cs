using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sedgewright.Models;

/// <summary>
/// Represents a chat message shown in the Sedgewright transcript.
/// </summary>
public sealed class MessageModel : INotifyPropertyChanged
{
    private string _content;
    private readonly string? _authorLabel;
    private bool _isInProgress;
    private bool _isExpanded;
    private bool _isToolActivity;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageModel"/> class.
    /// </summary>
    public MessageModel(bool isUser, string content, DateTime timestamp, string? authorLabel = null)
    {
        IsUser = isUser;
        _content = content;
        Timestamp = timestamp;
        _authorLabel = string.IsNullOrWhiteSpace(authorLabel) ? null : authorLabel.Trim();
    }

    /// <summary>
    /// Raised when one of the message properties changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets whether the message was authored by the user.
    /// </summary>
    public bool IsUser { get; }

    /// <summary>
    /// Gets or sets the tool call identifier associated with the message.
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// Gets whether the message contains provider-supplied reasoning content.
    /// </summary>
    public bool IsReasoning { get; init; }

    /// <summary>
    /// Gets whether the message should render as an assistant message.
    /// </summary>
    public bool IsAssistant => !IsUser && !IsToolActivity;

    /// <summary>
    /// Gets or sets whether the message represents tool activity.
    /// </summary>
    public bool IsToolActivity
    {
        get => _isToolActivity;
        set
        {
            if (!SetProperty(ref _isToolActivity, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsAssistant));
            OnPropertyChanged(nameof(AuthorLabel));
        }
    }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <summary>
    /// Gets whether the message represents a failure with expandable details.
    /// </summary>
    public bool IsError { get; init; }

    /// <summary>
    /// Gets diagnostic details shown only when an error message is expanded.
    /// </summary>
    public string? ErrorDetails { get; init; }

    /// <summary>
    /// Gets or sets whether the message is expanded in the UI.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    /// Appends incremental content to the message body.
    /// </summary>
    public void AppendContent(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        Content += content;
    }

    /// <summary>
    /// Gets or sets whether the message is still streaming.
    /// </summary>
    public bool IsInProgress
    {
        get => _isInProgress;
        set
        {
            if (!SetProperty(ref _isInProgress, value))
            {
                return;
            }

            OnPropertyChanged(nameof(AuthorLabel));
        }
    }

    /// <summary>
    /// Gets the status label for an in-progress assistant message.
    /// </summary>
    public string InProgressLabel => IsReasoning ? "thinking..." : "streaming...";

    /// <summary>
    /// Gets the message timestamp.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the timestamp formatted for display.
    /// </summary>
    public string TimestampText => Timestamp.ToString("t");

    /// <summary>
    /// Gets the display label for the author.
    /// </summary>
    public string AuthorLabel => IsUser
        ? "You"
        : IsToolActivity
            ? "Tool"
            : IsReasoning
                ? $"{_authorLabel ?? "Provider"} reasoning (provider-supplied)"
            : _authorLabel ?? (IsInProgress
                ? "Assistant - responding..."
                : "Assistant");

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
