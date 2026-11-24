using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sedgewright.ViewModels.Shared;

/// <summary>
/// Wraps an asynchronous action in an ICommand implementation.
/// </summary>
public sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
        => !_isExecuting && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
        => await ExecuteAsync();

    private async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Raises CanExecuteChanged so the UI can requery the command state.
    /// </summary>
    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
