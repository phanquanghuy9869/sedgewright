using Avalonia.Controls;
using Avalonia.Interactivity;
using Sedgewright.Models;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Chat;

/// <summary>
/// Presents an intermediate chat activity update with optional progress and details.
/// </summary>
public partial class ChatActivityView : UserControl
{
    /// <summary>
    /// Initializes a typed chat activity view.
    /// </summary>
    public ChatActivityView()
    {
        InitializeComponent();
    }

    private async void OnShowDetailsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IntermediateUpdate activity
            || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        ChatActivityDetailsWindow detailsWindow = new(activity);
        await detailsWindow.ShowDialog(owner);
    }
}
