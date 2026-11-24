using System;
using Avalonia.Controls;
using Sedgewright.Entities;
using Sedgewright.ViewModels.Chat;
using Sedgewright.Views.Shared;

namespace Sedgewright.Views.Chat;

/// <summary>
/// Modal window for selecting a chat target.
/// </summary>
public partial class ChatTargetPickerWindow : Window
{
    private ChatTargetPickerViewModel? _viewModel;

    /// <summary>
    /// Creates the picker window.
    /// </summary>
    public ChatTargetPickerWindow()
    {
        InitializeComponent();
        DialogWindowBehavior.Attach(this, TitleDragRegion);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as ChatTargetPickerViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(IChatTarget? target)
        => Close(target);
}
