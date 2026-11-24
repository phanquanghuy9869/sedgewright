using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage.Configurations;

/// <summary>
/// Configures skill selections attached to an agent.
/// </summary>
public sealed class AgentSkillConfiguration : IEntityTypeConfiguration<AgentSkillDefinition>
{
    public void Configure(EntityTypeBuilder<AgentSkillDefinition> entity)
    {
        entity.ToTable("AgentSkills");
        entity.HasKey(x => new { x.AgentId, x.SkillDirectory });

        entity.Property(x => x.SkillDirectory)
            .IsRequired()
            .HasMaxLength(2048);

        ValueConverter<List<string>, string> selectedSkillsConverter = new(
            selectedSkills => JsonSerializer.Serialize(selectedSkills),
            json => JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>());
        ValueComparer<List<string>> selectedSkillsComparer = new(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            selectedSkills => JsonSerializer.Serialize(selectedSkills).GetHashCode(),
            selectedSkills => new List<string>(selectedSkills));

        entity.Property(x => x.SelectedSkills)
            .IsRequired()
            .HasMaxLength(32768)
            .HasConversion(selectedSkillsConverter)
            .Metadata.SetValueComparer(selectedSkillsComparer);

        entity.HasOne(x => x.AgentDefinition)
            .WithMany(x => x.Skills)
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
