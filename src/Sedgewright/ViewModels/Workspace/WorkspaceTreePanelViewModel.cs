using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.Workspace;

/// <summary>
/// Owns the workspace tree shown in the right-side panel. The root is the working directory saved in Settings,
/// and the tree is rebuilt only when that directory changes.
/// </summary>
public sealed class WorkspaceTreePanelViewModel : ViewModelBase
{
    private readonly ISharedState _sharedState;
    private readonly IFileSystemService _fileSystemService;
    private CancellationTokenSource _treeCancellation = new();
    private CancellationTokenSource _fileReadCancellation = new();
    private string _rootPath = string.Empty;
    private WorkspaceTreeItemViewModel? _selectedItem;
    private string? _activeFilePath;
    private string _activeFileContent = string.Empty;
    private string _activeFileError = string.Empty;
    private bool _isFileLoading;

    /// <summary>
    /// Creates the workspace tree panel view model and subscribes to settings changes.
    /// </summary>
    /// <param name="sharedState">The shared state that provides the saved working directory.</param>
    /// <param name="fileSystemService">The service used to read folder contents lazily.</param>
    public WorkspaceTreePanelViewModel(ISharedState sharedState, IFileSystemService fileSystemService)
    {
        _sharedState = sharedState;
        _fileSystemService = fileSystemService;
        _sharedState.ApplicationSettingsChanged += OnApplicationSettingsChanged;
        CloseFileCommand = new RelayCommand(CloseFile);
    }

    /// <summary>
    /// Gets the top-level rows of the tree. It holds one row for the working directory.
    /// </summary>
    public ObservableCollection<WorkspaceTreeItemViewModel> RootItems { get; } = [];

    /// <summary>
    /// Gets the absolute path of the directory the tree is rooted at.
    /// </summary>
    public string RootPath => _rootPath;

    /// <summary>
    /// Gets or sets the row currently selected in the tree.
    /// </summary>
    public WorkspaceTreeItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetProperty(ref _selectedItem, value))
            {
                return;
            }

            if (value is { IsDirectory: false, IsPlaceholder: false })
            {
                OpenFile(value);
            }
        }
    }

    /// <summary>
    /// Gets the absolute path of the file currently displayed in the editor.
    /// </summary>
    public string? ActiveFilePath
    {
        get => _activeFilePath;
        private set
        {
            if (SetProperty(ref _activeFilePath, value))
            {
                OnPropertyChanged(nameof(IsFileOpen));
                OnPropertyChanged(nameof(ActiveFileName));
            }
        }
    }

    /// <summary>
    /// Gets the name of the file currently displayed in the editor.
    /// </summary>
    public string ActiveFileName =>
        string.IsNullOrWhiteSpace(ActiveFilePath)
            ? string.Empty
            : Path.GetFileName(ActiveFilePath);

    /// <summary>
    /// Gets the text content loaded for the active file.
    /// </summary>
    public string ActiveFileContent
    {
        get => _activeFileContent;
        private set => SetProperty(ref _activeFileContent, value);
    }

    /// <summary>
    /// Gets the read error for the active file.
    /// </summary>
    public string ActiveFileError
    {
        get => _activeFileError;
        private set
        {
            if (SetProperty(ref _activeFileError, value))
            {
                OnPropertyChanged(nameof(HasActiveFileError));
            }
        }
    }

    /// <summary>
    /// Gets whether the active file could not be read.
    /// </summary>
    public bool HasActiveFileError => !string.IsNullOrWhiteSpace(ActiveFileError);

    /// <summary>
    /// Gets whether a file is open in the viewer.
    /// </summary>
    public bool IsFileOpen => !string.IsNullOrWhiteSpace(ActiveFilePath);

    /// <summary>
    /// Gets whether the active file is being read.
    /// </summary>
    public bool IsFileLoading
    {
        get => _isFileLoading;
        private set => SetProperty(ref _isFileLoading, value);
    }

    /// <summary>
    /// Gets the command that closes the active file viewer.
    /// </summary>
    public ICommand CloseFileCommand { get; }

    /// <summary>
    /// Builds the tree from the working directory currently saved in Settings.
    /// </summary>
    public void Initialize()
        => OpenRoot(ResolveRootPath());

    private void OnApplicationSettingsChanged(object? sender, EventArgs e)
    {
        string rootPath = ResolveRootPath();
        if (string.Equals(rootPath, _rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        OpenRoot(rootPath);
    }

    private void OpenRoot(string rootPath)
    {
        // Cancel loads still running for the previous root so they do not update rows that are no longer shown.
        _treeCancellation.Cancel();
        _treeCancellation = new CancellationTokenSource();
        CloseFile();

        _rootPath = rootPath;
        SelectedItem = null;
        RootItems.Clear();

        WorkspaceTreeItemViewModel root = WorkspaceTreeItemViewModel.CreateDirectory(
            GetDisplayName(rootPath),
            rootPath,
            _fileSystemService,
            _treeCancellation.Token);

        RootItems.Add(root);
        OnPropertyChanged(nameof(RootPath));

        root.IsExpanded = true;
    }

    private void OpenFile(WorkspaceTreeItemViewModel item)
        => _ = OpenFileAsync(item);

    private async Task OpenFileAsync(WorkspaceTreeItemViewModel item)
    {
        _fileReadCancellation.Cancel();
        _fileReadCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _fileReadCancellation.Token;

        ActiveFilePath = item.FullPath;
        ActiveFileContent = string.Empty;
        ActiveFileError = string.Empty;
        IsFileLoading = true;

        try
        {
            Result<string, IApplicationError> result =
                await _fileSystemService.ReadTextFileAsync(item.FullPath, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                ActiveFileContent = string.Empty;
                ActiveFileError = string.Join(" ", result.Error.Messages);
                return;
            }

            ActiveFileContent = result.Value;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsFileLoading = false;
            }
        }
    }

    private void CloseFile()
    {
        _fileReadCancellation.Cancel();
        ActiveFilePath = null;
        ActiveFileContent = string.Empty;
        ActiveFileError = string.Empty;
        IsFileLoading = false;
    }

    private string ResolveRootPath()
    {
        string? savedDirectory = _sharedState.ApplicationSettings?.DefaultWorkingDirectory;
        return string.IsNullOrWhiteSpace(savedDirectory)
            ? Environment.CurrentDirectory
            : savedDirectory.Trim();
    }

    private static string GetDisplayName(string path)
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return string.IsNullOrEmpty(name) ? path : name;
    }
}
