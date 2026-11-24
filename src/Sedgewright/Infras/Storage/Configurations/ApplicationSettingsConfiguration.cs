using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;
using Sedgewright.Models;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the SQLite mapping for <see cref="ApplicationSettings"/>.
/// </summary>
public sealed class ApplicationSettingsConfiguration : IEntityTypeConfiguration<ApplicationSettings>
{
    public void Configure(EntityTypeBuilder<ApplicationSettings> entity)
    {
        entity.ToTable("ApplicationSettings");
        entity.HasKey(settings => settings.Id);
        entity.Property(settings => settings.Id).ValueGeneratedNever();
        entity.Property(settings => settings.DefaultWorkingDirectory)
            .IsRequired()
            .HasMaxLength(4096);
        entity.Property(settings => settings.ThemeMode)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(16);
        entity.Property(settings => settings.LastChatTargetType)
            .HasConversion<string?>()
            .HasMaxLength(32);
    }
}
