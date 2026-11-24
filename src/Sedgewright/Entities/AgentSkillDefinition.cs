using System.Collections.Generic;

namespace Sedgewright.Entities;

public sealed class AgentSkillDefinition
{
    public int AgentId { get; set; }
    public required string SkillDirectory { get; set; }
    public List<string> SelectedSkills { get; set; } = [];
    public AgentDefinition AgentDefinition { get; set; } = null!;

    public bool HasSkills => SelectedSkills is not null && SelectedSkills.Count > 0;

    public bool HasNoSkills => !HasSkills;
}
