using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// Persists the singleton application settings record to SQLite.
/// </summary>
public sealed class ApplicationSettingsRepository(SedgewrightDbContext dbContext)
    : IApplicationSettingsRepository
{
    private readonly SedgewrightDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<ApplicationSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        ApplicationSettings? settings = await _dbContext.ApplicationSettings
            .SingleOrDefaultAsync(item => item.Id == ApplicationSettings.SingletonId, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new ApplicationSettings();
        _dbContext.ApplicationSettings.Add(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ApplicationSettings? persistedSettings = await _dbContext.ApplicationSettings
            .SingleOrDefaultAsync(item => item.Id == ApplicationSettings.SingletonId, cancellationToken);

        if (persistedSettings is null)
        {
            settings.Id = ApplicationSettings.SingletonId;
            _dbContext.ApplicationSettings.Add(settings);
        }
        else
        {
            persistedSettings.DefaultWorkingDirectory = settings.DefaultWorkingDirectory;
            persistedSettings.ThemeMode = settings.ThemeMode;
            persistedSettings.LastChatTargetType = settings.LastChatTargetType;
            persistedSettings.LastChatTargetId = settings.LastChatTargetId;
            persistedSettings.LastAgentSessionId = settings.LastAgentSessionId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
