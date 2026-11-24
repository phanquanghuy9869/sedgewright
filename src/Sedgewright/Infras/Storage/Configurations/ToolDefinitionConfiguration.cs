using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

public sealed class ToolDefinitionConfiguration : IEntityTypeConfiguration<ToolDefinition>
{
    public void Configure(EntityTypeBuilder<ToolDefinition> entity)
    {
        entity.ToTable("ToolDefinitions");
        entity.HasKey(tool => tool.Id);
        entity.Property(tool => tool.Id)
            .ValueGeneratedOnAdd()
            .HasValueGenerator<GuidV7Generator>();
        entity.Property(tool => tool.Name).IsRequired().HasMaxLength(256);
        entity.Property(tool => tool.ToolDescription)
            .IsRequired(false)
            .HasMaxLength(500);
        entity.Property(tool => tool.Type)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(16);

        entity.HasOne(tool => tool.SequentialOrchestration)
            .WithMany(orchestration => orchestration.ToolDefinitions)
            .HasForeignKey(tool => tool.SequentialOrchestrationId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(tool => tool.ConcurrentOrchestration)
            .WithMany(orchestration => orchestration.ToolDefinitions)
            .HasForeignKey(tool => tool.ConcurrentOrchestrationId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(tool => tool.GroupChatOrchestration)
            .WithMany(orchestration => orchestration.ToolDefinitions)
            .HasForeignKey(tool => tool.GroupChatOrchestrationId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(tool => tool.MagenticOrchestration)
            .WithMany(orchestration => orchestration.ToolDefinitions)
            .HasForeignKey(tool => tool.MagenticOrchestrationId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(tool => tool.HandoffOrchestration)
            .WithMany(orchestration => orchestration.ToolDefinitions)
            .HasForeignKey(tool => tool.HandoffOrchestrationId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(tool => tool.Name).IsUnique();
    }
}

public sealed class GuidV7Generator : ValueGenerator<Guid>
{
    public override bool GeneratesTemporaryValues => false;

    public override Guid Next(EntityEntry entry)
        => Guid.CreateVersion7();
}
