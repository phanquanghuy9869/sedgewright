namespace Sedgewright.Entities;

public sealed class McpDefinition
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsStdio { get; set; }
    public required string ConfigJson { get; set; }
}
