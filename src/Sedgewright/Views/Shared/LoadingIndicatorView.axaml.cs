using Avalonia.Controls;

namespace Sedgewright.Views.Shared;

/// <summary>
/// Displays reusable inline loading feedback with optional cancellation.
/// </summary>
public partial class LoadingIndicatorView : UserControl
{
    /// <summary>
    /// Creates the loading indicator view.
    /// </summary>
    public LoadingIndicatorView()
    {
        InitializeComponent();
    }
}
