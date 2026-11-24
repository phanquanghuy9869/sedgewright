using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Agents.AI;
using NUnit.Framework;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Models;
using Sedgewright.ViewModels.Workspace;

namespace Sedgewright.Tests.ViewModels.Workspace;

[TestFixture]
public sealed class WorkspaceTreePanelViewModelTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "sentinel-workspace");
    private static readonly string SourceFolder = Path.Combine(Root, "src");

    [Test]
    public async Task Initialize_OpensSavedWorkingDirectory_AndReadsOnlyTheRootLevel()
    {
        FakeFileSystemService fileSystem = new();
        fileSystem.AddDirectory(Root,
            new FileSystemEntry("src", SourceFolder, IsDirectory: true),
            new FileSystemEntry("README.md", Path.Combine(Root, "README.md"), IsDirectory: false));
        FakeSharedState sharedState = new(new ApplicationSettings { DefaultWorkingDirectory = Root });
        WorkspaceTreePanelViewModel viewModel = new(sharedState, fileSystem);

        viewModel.Initialize();
        await viewModel.RootItems.Single().LoadChildrenAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.RootPath, Is.EqualTo(Root));
            Assert.That(viewModel.RootItems.Single().Name, Is.EqualTo("sentinel-workspace"));
            Assert.That(viewModel.RootItems.Single().Children.Select(child => child.Name), Is.EqualTo(new[] { "src", "README.md" }));
            Assert.That(fileSystem.RequestedDirectories, Is.EqualTo(new[] { Root }));
        }
    }

    [Test]
    public async Task ExpandingFolder_LoadsItsChildrenOnlyOnce()
    {
        FakeFileSystemService fileSystem = new();
        fileSystem.AddDirectory(Root, new FileSystemEntry("src", SourceFolder, IsDirectory: true));
        fileSystem.AddDirectory(SourceFolder, new FileSystemEntry("App.axaml", Path.Combine(SourceFolder, "App.axaml"), IsDirectory: false));
        WorkspaceTreePanelViewModel viewModel = new(new FakeSharedState(new ApplicationSettings { DefaultWorkingDirectory = Root }), fileSystem);
        viewModel.Initialize();
        await viewModel.RootItems.Single().LoadChildrenAsync();
        WorkspaceTreeItemViewModel source = viewModel.RootItems.Single().Children.Single(child => child.Name == "src");

        source.IsExpanded = true;
        source.IsExpanded = false;
        source.IsExpanded = true;
        await source.LoadChildrenAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fileSystem.RequestedDirectories.Count(path => path == SourceFolder), Is.EqualTo(1));
            Assert.That(source.Children.Select(child => child.Name), Is.EqualTo(new[] { "App.axaml" }));
        }
    }

    [Test]
    public async Task MissingWorkingDirectory_ShowsErrorOnRowWithoutThrowing()
    {
        FakeFileSystemService fileSystem = new();
        WorkspaceTreePanelViewModel viewModel = new(new FakeSharedState(new ApplicationSettings { DefaultWorkingDirectory = Root }), fileSystem);

        viewModel.Initialize();
        WorkspaceTreeItemViewModel root = viewModel.RootItems.Single();
        await root.LoadChildrenAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.ErrorMessage, Is.Not.Empty);
            Assert.That(root.Children, Is.Empty);
        }
    }

    [Test]
    public void SettingsChange_RebuildsTreeOnlyWhenRootPathChanges()
    {
        string otherRoot = Path.Combine(Path.GetTempPath(), "sentinel-other");
        FakeFileSystemService fileSystem = new();
        fileSystem.AddDirectory(Root);
        fileSystem.AddDirectory(otherRoot);
        FakeSharedState sharedState = new(new ApplicationSettings { DefaultWorkingDirectory = Root });
        WorkspaceTreePanelViewModel viewModel = new(sharedState, fileSystem);
        viewModel.Initialize();

        sharedState.Replace(new ApplicationSettings { DefaultWorkingDirectory = Root });
        sharedState.RaiseApplicationSettingsChanged();
        int requestsForSameRoot = fileSystem.RequestedDirectories.Count(path => path == Root);

        sharedState.Replace(new ApplicationSettings { DefaultWorkingDirectory = otherRoot });
        sharedState.RaiseApplicationSettingsChanged();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requestsForSameRoot, Is.EqualTo(1));
            Assert.That(viewModel.RootPath, Is.EqualTo(otherRoot));
            Assert.That(viewModel.RootItems.Single().FullPath, Is.EqualTo(otherRoot));
        }
    }

    [Test]
    public async Task SelectingFile_OpensReadOnlyTextContent()
    {
        string filePath = Path.Combine(Root, "README.md");
        FakeFileSystemService fileSystem = new();
        fileSystem.AddDirectory(Root, new FileSystemEntry("README.md", filePath, IsDirectory: false));
        fileSystem.AddTextFile(filePath, "# Sedgewright");
        WorkspaceTreePanelViewModel viewModel = new(
            new FakeSharedState(new ApplicationSettings { DefaultWorkingDirectory = Root }),
            fileSystem);
        viewModel.Initialize();
        await viewModel.RootItems.Single().LoadChildrenAsync();

        viewModel.SelectedItem = viewModel.RootItems.Single().Children.Single();
        await WaitForFileReadAsync(viewModel);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.IsFileOpen, Is.True);
            Assert.That(viewModel.ActiveFilePath, Is.EqualTo(filePath));
            Assert.That(viewModel.ActiveFileName, Is.EqualTo("README.md"));
            Assert.That(viewModel.ActiveFileContent, Is.EqualTo("# Sedgewright"));
            Assert.That(viewModel.HasActiveFileError, Is.False);
        }
    }

    [Test]
    public async Task SelectingUnreadableFile_ShowsFailureWithoutOpeningContent()
    {
        string filePath = Path.Combine(Root, "missing.bin");
        FakeFileSystemService fileSystem = new();
        fileSystem.AddDirectory(Root, new FileSystemEntry("missing.bin", filePath, IsDirectory: false));
        WorkspaceTreePanelViewModel viewModel = new(
            new FakeSharedState(new ApplicationSettings { DefaultWorkingDirectory = Root }),
            fileSystem);
        viewModel.Initialize();
        await viewModel.RootItems.Single().LoadChildrenAsync();

        viewModel.SelectedItem = viewModel.RootItems.Single().Children.Single();
        await WaitForFileReadAsync(viewModel);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.IsFileOpen, Is.True);
            Assert.That(viewModel.ActiveFileContent, Is.Empty);
            Assert.That(viewModel.HasActiveFileError, Is.True);
            Assert.That(viewModel.ActiveFileError, Does.Contain("does not exist"));
        }
    }

    [Test]
    public async Task CloseFileCommand_ClearsActiveFile()
    {
        string filePath = Path.Combine(Root, "README.md");
        FakeFileSystemService fileSystem = new();
        fileSystem.AddDirectory(Root, new FileSystemEntry("README.md", filePath, IsDirectory: false));
        fileSystem.AddTextFile(filePath, "# Sedgewright");
        WorkspaceTreePanelViewModel viewModel = new(
            new FakeSharedState(new ApplicationSettings { DefaultWorkingDirectory = Root }),
            fileSystem);
        viewModel.Initialize();
        await viewModel.RootItems.Single().LoadChildrenAsync();
        viewModel.SelectedItem = viewModel.RootItems.Single().Children.Single();
        await WaitForFileReadAsync(viewModel);

        viewModel.CloseFileCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.IsFileOpen, Is.False);
            Assert.That(viewModel.ActiveFilePath, Is.Null);
            Assert.That(viewModel.ActiveFileContent, Is.Empty);
            Assert.That(viewModel.ActiveFileError, Is.Empty);
        }
    }

    private static async Task WaitForFileReadAsync(WorkspaceTreePanelViewModel viewModel)
    {
        for (int attempt = 0; attempt < 20 && viewModel.IsFileLoading; attempt++)
        {
            await Task.Delay(10);
        }
    }

    private sealed class FakeFileSystemService : IFileSystemService
    {
        private readonly Dictionary<string, IReadOnlyList<FileSystemEntry>> _children = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _textFiles = new(StringComparer.OrdinalIgnoreCase);

        public List<string> RequestedDirectories { get; } = [];

        public void AddDirectory(string path, params FileSystemEntry[] children)
            => _children[path] = children;

        public void AddTextFile(string path, string content)
            => _textFiles[path] = content;

        public Task<Result<IReadOnlyList<FileSystemEntry>, IApplicationError>> GetChildrenAsync(
            string directoryPath,
            CancellationToken cancellationToken = default)
        {
            RequestedDirectories.Add(directoryPath);

            if (_children.TryGetValue(directoryPath, out IReadOnlyList<FileSystemEntry>? entries))
            {
                return Task.FromResult(Result.Success<IReadOnlyList<FileSystemEntry>, IApplicationError>(entries));
            }

            IApplicationError error = new GenericError([$"The directory does not exist: {directoryPath}"]);
            return Task.FromResult(Result.Failure<IReadOnlyList<FileSystemEntry>, IApplicationError>(error));
        }

        public Task<Result<string, IApplicationError>> ReadTextFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (_textFiles.TryGetValue(filePath, out string? content))
            {
                return Task.FromResult(Result.Success<string, IApplicationError>(content));
            }

            IApplicationError error = new GenericError([$"The file does not exist: {filePath}"]);
            return Task.FromResult(Result.Failure<string, IApplicationError>(error));
        }
    }

    private sealed class FakeSharedState(ApplicationSettings settings) : ISharedState
    {
        private ApplicationSettings _applicationSettings = settings;

        public event EventHandler? ApplicationSettingsChanged;

        public ApplicationSettings? ApplicationSettings => _applicationSettings;

        public void Replace(ApplicationSettings settings)
            => _applicationSettings = settings;

        public void RaiseApplicationSettingsChanged()
            => ApplicationSettingsChanged?.Invoke(this, EventArgs.Empty);

        public ObservableCollection<AgentDefinitionSnapshot> Agents { get; } = [];

        public Maybe<AgentRuntime> AgentRuntime => Maybe<AgentRuntime>.None;

        public ObservableCollection<AgentSessionSnapshot> AgentSessions { get; } = [];

        public ObservableCollection<OrchestrationListItemSnapshot> Orchestrations { get; } = [];

        public ObservableCollection<LLMProvider> Providers { get; } = [];

        public Task RefreshAgentsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void SetAgentSessions(IEnumerable<AgentSessionSnapshot> agentSessions)
        {
        }

        public Task SetAgentRuntimeAsync(AgentRuntime agentRuntime)
            => Task.CompletedTask;

        public Task RefreshOrchestrationsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RefreshProvidersAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RefreshApplicationSettingsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveApplicationSettingsAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
        {
            _applicationSettings = settings;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
