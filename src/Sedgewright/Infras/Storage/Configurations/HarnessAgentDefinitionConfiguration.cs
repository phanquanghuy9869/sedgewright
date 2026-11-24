using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures the <see cref="HarnessAgentDefinition"/> table-per-type extension of
/// <c>AgentDefinitions</c>, holding the extra Harness pipeline toggles.
/// </summary>
public sealed class HarnessAgentDefinitionConfiguration : IEntityTypeConfiguration<HarnessAgentDefinition>
{
    public void Configure(EntityTypeBuilder<HarnessAgentDefinition> entity)
    {
        entity.ToTable("HarnessAgentDefinitions");

        entity.Property(x => x.FileAccessWorkingDirectory).HasMaxLength(1000);
    }
}
