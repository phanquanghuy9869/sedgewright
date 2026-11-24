using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Models.Updates;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Sedgewright.Infras.Services;

public sealed class UpdateService : IUpdateService
{
    private const string GitHubRepositoryUrl = "https://github.com/huy-phan-prospa/sentinel";
    private readonly UpdateManager? _updateManager;
    private UpdateInfo? _availableUpdate;

    public UpdateService()
    {
        CurrentVersion = ResolveCurrentVersion();

        try
        {
            if (!HasVelopackInstallation())
            {
                _updateManager = null;
                return;
            }

            _updateManager = new UpdateManager(
                new GithubSource(GitHubRepositoryUrl, accessToken: null, prerelease: false));
        }
        catch (InvalidOperationException)
        {
            // A direct Debug launch has no Velopack installation context.
            _updateManager = null;
        }
    }

    public string CurrentVersion { get; }

    private static string ResolveCurrentVersion()
    {
        try
        {
            return VelopackLocator.Current.CurrentlyInstalledVersion?.ToString() ?? "0.0.0";
        }
        catch (InvalidOperationException)
        {
            return "0.0.0";
        }
    }

    private static bool HasVelopackInstallation()
    {
        try
        {
            return VelopackLocator.Current.CurrentlyInstalledVersion is not null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (_updateManager is null)
        {
            return new UpdateCheckResult(
                IsUpdateAvailable: false,
                CurrentVersion,
                AvailableVersion: null);
        }

        try
        {
            _availableUpdate = await _updateManager.CheckForUpdatesAsync().WaitAsync(cancellationToken);
            string? availableVersion = _availableUpdate?.TargetFullRelease.Version.ToString();

            return new UpdateCheckResult(
                IsUpdateAvailable: _availableUpdate is not null,
                CurrentVersion,
                availableVersion);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _availableUpdate = null;
            return new UpdateCheckResult(
                IsUpdateAvailable: false,
                CurrentVersion,
                AvailableVersion: null,
                ErrorMessage: exception.Message);
        }
    }

    public async Task ApplyUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (_updateManager is null)
        {
            throw new InvalidOperationException("Updates are only available for Velopack-installed builds.");
        }

        UpdateInfo availableUpdate = _availableUpdate
            ?? throw new InvalidOperationException("No application update is available.");

        await _updateManager.DownloadUpdatesAsync(availableUpdate).WaitAsync(cancellationToken);
        _updateManager.ApplyUpdatesAndRestart(availableUpdate);
    }

}
