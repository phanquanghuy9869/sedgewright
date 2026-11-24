using Avalonia.Controls;

namespace Sedgewright.Views.Workspace;

/// <summary>
/// Displays a lazy-loaded, read-only tree of the working directory saved in Settings.
/// The tree is bound to <see cref="ViewModels.Workspace.WorkspaceTreePanelViewModel"/>; this code-behind holds no logic.
/// </summary>
public sealed partial class WorkspaceTreePanelView : UserControl
{
    /// <summary>
    /// Creates the workspace tree panel view.
    /// </summary>
    public WorkspaceTreePanelView()
    {
        InitializeComponent();
    }
}
