using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using Sedgewright.Infras.Services;
using Sedgewright.Models;

namespace Sedgewright.Tests.Infras.Services;

[TestFixture]
public sealed class FileSystemServiceTests
{
    [Test]
    public async Task GetChildrenAsync_ReturnsOnlyImmediateChildren_FoldersFirstThenByName()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src", "Features"));
            File.WriteAllText(Path.Combine(root, "src", "Features", "Deep.cs"), string.Empty);
            Directory.CreateDirectory(Path.Combine(root, "Assets"));
            File.WriteAllText(Path.Combine(root, "README.md"), string.Empty);
            File.WriteAllText(Path.Combine(root, "App.axaml"), string.Empty);

            FileSystemService service = new();
            Result<IReadOnlyList<FileSystemEntry>, IApplicationError> result = await service.GetChildrenAsync(root);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                result.Value.Select(entry => (entry.Name, entry.IsDirectory)).ToArray(),
                Is.EqualTo(new[] { ("Assets", true), ("src", true), ("App.axaml", false), ("README.md", false) }));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task GetChildrenAsync_MissingDirectory_ReturnsFailureInsteadOfThrowing()
    {
        string missing = Path.Combine(Path.GetTempPath(), "sentinel-missing-" + Guid.NewGuid());

        FileSystemService service = new();
        Result<IReadOnlyList<FileSystemEntry>, IApplicationError> result = await service.GetChildrenAsync(missing);

        Assert.That(result.IsFailure, Is.True);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "sentinel-fs-" + Guid.NewGuid());
        Directory.CreateDirectory(path);
        return path;
    }
}
