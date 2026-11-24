namespace Sedgewright.Models;

/// <summary>
/// Describes one immediate child of a directory, either a file or a folder.
/// </summary>
/// <param name="Name">The file or folder name without its parent path.</param>
/// <param name="FullPath">The absolute path of the file or folder.</param>
/// <param name="IsDirectory">True when the entry is a folder.</param>
public sealed record FileSystemEntry(string Name, string FullPath, bool IsDirectory);
