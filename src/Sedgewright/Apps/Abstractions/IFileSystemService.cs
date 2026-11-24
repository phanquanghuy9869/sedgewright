using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Models;

namespace Sedgewright.Apps.Abstractions;

/// <summary>
/// Provides read-only access to the immediate contents of a directory for the workspace tree.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Lists only the immediate children of a directory, folders first and then files, each sorted by name.
    /// </summary>
    /// <param name="directoryPath">The absolute path of the directory to list.</param>
    /// <param name="cancellationToken">Cancels the enumeration when the tree is no longer displayed.</param>
    /// <returns>The children, or a failure when the directory is missing or cannot be read.</returns>
    Task<Result<IReadOnlyList<FileSystemEntry>, IApplicationError>> GetChildrenAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a local text file for display in the workspace file viewer.
    /// </summary>
    /// <param name="filePath">The absolute path of the file to read.</param>
    /// <param name="cancellationToken">Cancels the read while it is in progress.</param>
    /// <returns>The text content, or a failure when the file is missing, unreadable, binary, or too large.</returns>
    Task<Result<string, IApplicationError>> ReadTextFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
