using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Models;

namespace Sedgewright.Infras.Services;

/// <summary>
/// Lists immediate directory children from the local file system without recursing into subfolders.
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    private const long MaximumTextFileSizeBytes = 5 * 1024 * 1024;

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<FileSystemEntry>, IApplicationError>> GetChildrenAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
        => Task.Run(() => GetChildren(directoryPath, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<Result<string, IApplicationError>> ReadTextFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new ValidationError(["A file path is required."]);
        }

        try
        {
            FileInfo file = new(filePath.Trim());
            if (!file.Exists)
            {
                return new GenericError([$"The file does not exist: {file.FullName}"]);
            }

            if (file.Length > MaximumTextFileSizeBytes)
            {
                return new GenericError([$"The file is larger than 5 MB and will not be opened: {file.FullName}"]);
            }

            string content = await File.ReadAllTextAsync(file.FullName, cancellationToken);
            if (LooksLikeBinary(content))
            {
                return new GenericError([$"The file appears to be binary and cannot be shown as text: {file.FullName}"]);
            }

            return Result.Success<string, IApplicationError>(content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new GenericError([$"Could not read the file: {exception.Message}"]);
        }
    }

    private static bool LooksLikeBinary(string content)
    {
        foreach (char character in content)
        {
            if (char.IsControl(character) && character is not '\r' and not '\n' and not '\t')
            {
                return true;
            }
        }

        return false;
    }

    private static Result<IReadOnlyList<FileSystemEntry>, IApplicationError> GetChildren(
        string directoryPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return new ValidationError(["A directory path is required."]);
        }

        DirectoryInfo directory = new(directoryPath.Trim());
        if (!directory.Exists)
        {
            return new GenericError([$"The directory does not exist: {directory.FullName}"]);
        }

        try
        {
            List<FileSystemEntry> entries = [];
            foreach (FileSystemInfo info in directory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(new FileSystemEntry(info.Name, info.FullName, info is DirectoryInfo));
            }

            IReadOnlyList<FileSystemEntry> orderedEntries = entries
                .OrderByDescending(entry => entry.IsDirectory)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Result.Success<IReadOnlyList<FileSystemEntry>, IApplicationError>(orderedEntries);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new GenericError([$"Could not read the directory: {exception.Message}"]);
        }
    }
}
