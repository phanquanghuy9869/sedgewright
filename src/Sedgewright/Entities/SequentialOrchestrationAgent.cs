namespace Sedgewright.Entities;

public sealed class SequentialOrchestrationAgent
{
    public int SequentialOrchestrationId { get; set; }
    public int AgentDefinitionId { get; set; }
    public int StepOrder { get; set; }
    public AgentDefinition AgentDefinition { get; set; } = null!;
    public SequentialOrchestration SequentialOrchestration { get; set; } = null!;
}
