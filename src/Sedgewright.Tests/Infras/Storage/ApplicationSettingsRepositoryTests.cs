using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Entities;
using Sedgewright.Infras.Storage;

namespace Sedgewright.Tests.Infras.Storage;

[TestFixture]
public sealed class ApplicationSettingsRepositoryTests
{
    [Test]
    public async Task GetOrCreateAsync_CreatesSingletonAndSaveAsyncPersistsValues()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (SedgewrightDbContext context = new(options))
        {
            await context.Database.EnsureCreatedAsync();
            ApplicationSettingsRepository repository = new(context);

            ApplicationSettings settings = await repository.GetOrCreateAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings.Id, Is.EqualTo(ApplicationSettings.SingletonId));
                Assert.That(settings.DefaultWorkingDirectory, Is.Empty);
                Assert.That(settings.ThemeMode, Is.EqualTo(ThemeMode.Light));
            }

            ApplicationSettings changedSettings = new()
            {
                DefaultWorkingDirectory = "d:\\workspace",
                ThemeMode = ThemeMode.Dark,
            };
            await repository.SaveAsync(changedSettings);
        }

        await using (SedgewrightDbContext context = new(options))
        {
            ApplicationSettingsRepository repository = new(context);
            ApplicationSettings settings = await repository.GetOrCreateAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings.Id, Is.EqualTo(ApplicationSettings.SingletonId));
                Assert.That(settings.DefaultWorkingDirectory, Is.EqualTo("d:\\workspace"));
                Assert.That(settings.ThemeMode, Is.EqualTo(ThemeMode.Dark));
            }
        }
    }
}
