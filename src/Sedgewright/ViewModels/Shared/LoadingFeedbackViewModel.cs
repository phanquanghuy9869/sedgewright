using System;
using System.Threading;
using System.Windows.Input;

namespace Sedgewright.ViewModels.Shared;

/// <summary>
/// Tracks an asynchronous operation's inline loading message and optional cancellation.
/// </summary>
public sealed class LoadingFeedbackViewModel : ViewModelBase
{
    private readonly RelayCommand _cancelCommand;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _allowCancellation;
    private bool _isCancellationRequested;
    private bool _isLoading;
    private string _message = string.Empty;

    /// <summary>
    /// Creates an idle loading-feedback state.
    /// </summary>
    public LoadingFeedbackViewModel()
    {
        _cancelCommand = new RelayCommand(Cancel, () => CanCancel);
    }

    /// <summary>
    /// Gets whether an operation is currently running.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanCancel));
            _cancelCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Gets the concise message shown beside the loading indicator.
    /// </summary>
    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>
    /// Gets whether the active operation can still be canceled.
    /// </summary>
    public bool CanCancel => IsLoading
        && _allowCancellation
        && !_isCancellationRequested
        && _cancellationTokenSource is not null;

    /// <summary>
    /// Gets the command that requests cancellation of the active operation.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>
    /// Starts displaying loading feedback and returns the operation's cancellation token.
    /// </summary>
    /// <param name="message">The concise message to display while loading.</param>
    /// <param name="canCancel">Whether the operation supports cancellation.</param>
    /// <returns>The cancellation token to pass to the asynchronous operation.</returns>
    /// <exception cref="ArgumentException"><paramref name="message"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Another operation is already active.</exception>
    public CancellationToken Begin(string message, bool canCancel = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (IsLoading)
        {
            throw new InvalidOperationException("A loading operation is already active.");
        }

        _allowCancellation = canCancel;
        _isCancellationRequested = false;
        _cancellationTokenSource = canCancel ? new CancellationTokenSource() : null;
        Message = message;
        IsLoading = true;
        return _cancellationTokenSource?.Token ?? CancellationToken.None;
    }

    /// <summary>
    /// Hides the loading feedback and releases the active cancellation source.
    /// </summary>
    public void Complete()
    {
        if (!IsLoading)
        {
            return;
        }

        CancellationTokenSource? cancellationTokenSource = _cancellationTokenSource;
        _cancellationTokenSource = null;
        _allowCancellation = false;
        _isCancellationRequested = false;
        cancellationTokenSource?.Dispose();
        IsLoading = false;
        Message = string.Empty;
    }

    private void Cancel()
    {
        CancellationTokenSource? cancellationTokenSource = _cancellationTokenSource;
        if (!CanCancel || cancellationTokenSource is null)
        {
            return;
        }

        _isCancellationRequested = true;
        Message = "Canceling...";
        OnPropertyChanged(nameof(CanCancel));
        _cancelCommand.RaiseCanExecuteChanged();
        cancellationTokenSource.Cancel();
    }
}
