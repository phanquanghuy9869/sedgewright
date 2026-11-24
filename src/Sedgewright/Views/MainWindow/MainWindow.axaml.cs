using System;
using System.ComponentModel;
using Avalonia.Controls;
using Sedgewright.ViewModels.MainWindow;
using Sedgewright.ViewModels.Workspace;

namespace Sedgewright.Views.MainWindow;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private WorkspaceTreePanelViewModel? _workspaceTree;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_workspaceTree is not null)
        {
            _workspaceTree.PropertyChanged -= OnWorkspaceTreePropertyChanged;
        }

        _workspaceTree = (DataContext as MainWindowViewModel)?.WorkspaceTree;
        if (_workspaceTree is not null)
        {
            _workspaceTree.PropertyChanged += OnWorkspaceTreePropertyChanged;
            SyncFileEditor(_workspaceTree.ActiveFileContent);
        }
        else
        {
            SyncFileEditor(string.Empty);
        }
    }

    private void OnWorkspaceTreePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceTreePanelViewModel.ActiveFileContent))
        {
            SyncFileEditor(_workspaceTree?.ActiveFileContent ?? string.Empty);
        }
    }

    private void SyncFileEditor(string content)
    {
        if (FileEditor.Text != content)
        {
            FileEditor.Text = content;
        }
    }
}
