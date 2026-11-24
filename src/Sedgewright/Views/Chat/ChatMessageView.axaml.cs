using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Sedgewright.Models;

namespace Sedgewright.Views.Chat;

/// <summary>
/// Presents a chat message according to its role and current streaming state.
/// </summary>
public partial class ChatMessageView : UserControl
{
    /// <summary>
    /// Initializes a typed chat message view.
    /// </summary>
    public ChatMessageView()
    {
        InitializeComponent();
    }

    private void OnStreamingCardScrollViewerLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.LayoutUpdated += (_, _) => scrollViewer.ScrollToEnd();
        }
    }

    private async void OnCopyMessageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: MessageModel message })
        {
            return;
        }

        IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(message.Content);
        }
    }

    private void OnToggleMessageExpansionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: MessageModel message })
        {
            return;
        }

        message.IsExpanded = !message.IsExpanded;
    }
}
