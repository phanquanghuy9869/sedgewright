using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Sedgewright.ViewModels.Chat;

namespace Sedgewright.Views.Chat;

/// <summary>
/// Minimal chat surface for Sedgewright.
/// </summary>
public partial class ChatView : UserControl
{
    private readonly ScrollViewer? _messagesScrollViewer;
    private ChatViewModel? _subscribedViewModel;

    public ChatView()
    {
        InitializeComponent();

        _messagesScrollViewer = this.FindControl<ScrollViewer>("MessagesScrollViewer");

        TextBox? chatInput = this.FindControl<TextBox>("ChatInput");
        if (chatInput is not null)
        {
            chatInput.AddHandler(InputElement.KeyDownEvent, OnChatInputKeyDown, RoutingStrategies.Tunnel, true);
        }

        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Keeps the timeline subscription in sync with the active <see cref="ChatViewModel"/>.
    /// </summary>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.TimelineItems.CollectionChanged -= OnTimelineItemsCollectionChanged;
            _subscribedViewModel = null;
        }

        if (DataContext is ChatViewModel viewModel)
        {
            _subscribedViewModel = viewModel;
            viewModel.TimelineItems.CollectionChanged += OnTimelineItemsCollectionChanged;
        }
    }

    private void OnTimelineItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _messagesScrollViewer is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _messagesScrollViewer.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void OnChatInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (e.KeyModifiers & KeyModifiers.Shift) != 0)
        {
            return;
        }

        e.Handled = true;

        if (DataContext is not ChatViewModel viewModel)
        {
            return;
        }

        if (viewModel.SendCommand.CanExecute(null))
        {
            viewModel.SendCommand.Execute(null);
        }
    }

}

