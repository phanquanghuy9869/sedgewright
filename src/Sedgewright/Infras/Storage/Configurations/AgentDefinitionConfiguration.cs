using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the persisted agent definition aggregate root.
/// </summary>
public sealed class AgentDefinitionConfiguration : IEntityTypeConfiguration<AgentDefinition>
{
    public void Configure(EntityTypeBuilder<AgentDefinition> entity)
    {
        entity.ToTable("AgentDefinitions");
        entity.UseTptMappingStrategy();
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedOnAdd();
        entity.Property(x => x.Name).IsRequired();
        entity.Property(x => x.Description).IsRequired();
        entity.Property(x => x.Model).IsRequired().HasMaxLength(500);
        entity.OwnsOne(x => x.ShellConfiguration, shellConfiguration =>
        {
            shellConfiguration.Property(x => x.Executor)
                .HasColumnName("ShellExecutor")
                .HasConversion<string>()
                .IsRequired();
            shellConfiguration.Property(x => x.WorkingDirectory)
                .HasColumnName("ShellWorkingDirectory")
                .IsRequired();
            shellConfiguration.Property(x => x.IsActive)
                .HasColumnName("ShellIsActive")
                .IsRequired();
        });
        entity.Property(x => x.LLMProviderId).IsRequired();

        entity.HasOne(x => x.Provider)
            .WithMany(x => x.AgentDefinitions)
            .HasForeignKey(x => x.LLMProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasMany(x => x.Tools)
            .WithOne(x => x.AgentDefinition)
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.Skills)
            .WithOne(x => x.AgentDefinition)
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(x => x.McpAttachments)
            .WithOne(x => x.AgentDefinition)
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.Ignore(x => x.McpDefinitions);
        entity.Ignore(x => x.ToolDefinitions);

        entity.HasIndex(x => x.Name).IsUnique();
        entity.HasIndex(x => x.LLMProviderId);
    }
}
