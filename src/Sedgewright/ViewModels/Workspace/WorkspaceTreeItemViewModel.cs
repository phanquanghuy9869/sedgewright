using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.Workspace;

/// <summary>
/// Represents one file or folder row in the workspace tree and loads a folder's immediate children on first expansion.
/// </summary>
public sealed class WorkspaceTreeItemViewModel : ViewModelBase
{
    private static readonly Geometry FolderIcon = Geometry.Parse("M3 6.5A1.5 1.5 0 0 1 4.5 5h4.2l1.8 2h8A1.5 1.5 0 0 1 20 8.5v9a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 3 17.5Z");
    private static readonly Geometry OpenFolderIcon = Geometry.Parse("M3 6.5A1.5 1.5 0 0 1 4.5 5h4.2l1.8 2h8A1.5 1.5 0 0 1 20 8.5V10H6.5L3 17.5Z");
    private static readonly Geometry FileIcon = Geometry.Parse("M7 3h7l5 5v11a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z M14 3v5h5");

    private readonly IFileSystemService? _fileSystemService;
    private readonly CancellationToken _cancellationToken;
    private bool _isExpanded;
    private bool _isLoaded;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private Task? _loadTask;

    private WorkspaceTreeItemViewModel(
        string name,
        string fullPath,
        bool isDirectory,
        bool isPlaceholder,
        IFileSystemService? fileSystemService,
        CancellationToken cancellationToken)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        IsPlaceholder = isPlaceholder;
        _fileSystemService = fileSystemService;
        _cancellationToken = cancellationToken;

        if (isDirectory && fileSystemService is not null)
        {
            // The placeholder gives directories an expand arrow before their children are read.
            Children.Add(new WorkspaceTreeItemViewModel("Loading...", string.Empty, isDirectory: false, isPlaceholder: true, fileSystemService: null, cancellationToken: default));
        }
    }

    /// <summary>
    /// Gets the child rows shown beneath this item.
    /// </summary>
    public ObservableCollection<WorkspaceTreeItemViewModel> Children { get; } = [];

    /// <summary>
    /// Gets the file or folder name shown in the tree.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the absolute path of the file or folder.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Gets a value indicating whether this item represents a folder.
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// Gets a value indicating whether this item is the temporary row shown while a folder is loading.
    /// </summary>
    public bool IsPlaceholder { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the folder is expanded. Expanding an unloaded folder starts loading its children.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetProperty(ref _isExpanded, value))
            {
                return;
            }

            OnPropertyChanged(nameof(Icon));

            if (_isExpanded)
            {
                _ = LoadChildrenAsync();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the folder's immediate children have been read.
    /// </summary>
    public bool IsLoaded
    {
        get => _isLoaded;
        private set => SetProperty(ref _isLoaded, value);
    }

    /// <summary>
    /// Gets a value indicating whether the folder's children are currently being read.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// Gets the reason the folder could not be read, or an empty string when it loaded successfully.
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Gets the vector icon that matches the folder or file state.
    /// </summary>
    public Geometry Icon => !IsDirectory
        ? FileIcon
        : IsExpanded ? OpenFolderIcon : FolderIcon;

    /// <summary>
    /// Creates a row for a file or folder returned by the file system service.
    /// </summary>
    /// <param name="entry">The entry to display.</param>
    /// <param name="fileSystemService">The service used to read this folder's children on expansion.</param>
    /// <param name="cancellationToken">Cancels loading when the tree is replaced.</param>
    /// <returns>The row view model.</returns>
    public static WorkspaceTreeItemViewModel FromEntry(
        FileSystemEntry entry,
        IFileSystemService fileSystemService,
        CancellationToken cancellationToken)
        => new(entry.Name, entry.FullPath, entry.IsDirectory, isPlaceholder: false, fileSystemService, cancellationToken);

    /// <summary>
    /// Creates a folder row that is not yet loaded. Use this for the workspace root.
    /// </summary>
    /// <param name="name">The name shown for the folder.</param>
    /// <param name="fullPath">The absolute path of the folder.</param>
    /// <param name="fileSystemService">The service used to read this folder's children on expansion.</param>
    /// <param name="cancellationToken">Cancels loading when the tree is replaced.</param>
    /// <returns>The folder row view model.</returns>
    public static WorkspaceTreeItemViewModel CreateDirectory(
        string name,
        string fullPath,
        IFileSystemService fileSystemService,
        CancellationToken cancellationToken)
        => new(name, fullPath, isDirectory: true, isPlaceholder: false, fileSystemService, cancellationToken);

    /// <summary>
    /// Reads this folder's immediate children once. Repeated calls return the same in-flight or completed load.
    /// </summary>
    /// <returns>A task that completes when loading finishes. It never throws for filesystem failures.</returns>
    public Task LoadChildrenAsync()
        => _loadTask ??= LoadChildrenCoreAsync();

    private async Task LoadChildrenCoreAsync()
    {
        if (!IsDirectory || _fileSystemService is null)
        {
            return;
        }

        IsLoading = true;

        try
        {
            Result<IReadOnlyList<FileSystemEntry>, IApplicationError> result =
                await _fileSystemService.GetChildrenAsync(FullPath, _cancellationToken);

            Children.Clear();

            if (result.IsFailure)
            {
                ErrorMessage = string.Join(" ", result.Error.Messages);
                return;
            }

            foreach (FileSystemEntry entry in result.Value)
            {
                Children.Add(FromEntry(entry, _fileSystemService, _cancellationToken));
            }

            ErrorMessage = string.Empty;
            IsLoaded = true;
        }
        catch (OperationCanceledException)
        {
            // The tree was replaced, so this row is no longer displayed.
        }
        finally
        {
            IsLoading = false;
        }
    }
}
