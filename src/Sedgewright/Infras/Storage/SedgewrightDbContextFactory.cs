using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sedgewright.Infras.Storage;

namespace Sedgewright.Infras;

/// <summary>
/// Creates the UI database context at design time for EF Core migrations and tooling.
/// </summary>
public sealed class SedgewrightDbContextFactory : IDesignTimeDbContextFactory<SedgewrightDbContext>
{
    public SedgewrightDbContext CreateDbContext(string[] args)
    {
        string databasePath = StoragePathResolver.GetDefaultDatabasePath();

        string? databaseDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        DbContextOptions<SedgewrightDbContext> options = new DbContextOptionsBuilder<SedgewrightDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new SedgewrightDbContext(options);
    }
}
