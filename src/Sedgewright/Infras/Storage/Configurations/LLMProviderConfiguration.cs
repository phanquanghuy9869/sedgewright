using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the SQLite mapping for <see cref="LLMProvider"/>.
/// </summary>
public sealed class LLMProviderConfiguration : IEntityTypeConfiguration<LLMProvider>
{
    public void Configure(EntityTypeBuilder<LLMProvider> entity)
    {
        entity.ToTable("LLMProviders");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedOnAdd();
        entity.Property(x => x.ProviderKind).HasConversion<string>().IsRequired().HasMaxLength(128);
        entity.Property(x => x.DisplayName).IsRequired().HasMaxLength(128);
        entity.Property(x => x.BaseUrl).IsRequired().HasMaxLength(1024);
        entity.Property(x => x.ApiKey).IsRequired().HasMaxLength(4096);
        entity.Property(x => x.EnabledModelsJson).IsRequired().HasMaxLength(32768);
    }
}
