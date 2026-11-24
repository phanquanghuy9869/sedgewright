using Avalonia.Controls;
using Avalonia.Interactivity;
using Sedgewright.Models;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Chat;

/// <summary>
/// Shows the complete payload for a streamed chat activity.
/// </summary>
public partial class ChatActivityDetailsWindow : Window
{
    /// <summary>
    /// Creates the details dialog.
    /// </summary>
    public ChatActivityDetailsWindow()
    {
        InitializeComponent();
        DialogWindowBehavior.Attach(this, TitleDragRegion);
    }

    /// <summary>
    /// Creates a details dialog for the supplied chat activity.
    /// </summary>
    public ChatActivityDetailsWindow(IntermediateUpdate activity) : this()
    {
        DataContext = activity;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
        => Close();
}
