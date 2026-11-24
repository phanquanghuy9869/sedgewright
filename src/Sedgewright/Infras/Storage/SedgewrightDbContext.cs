using Microsoft.EntityFrameworkCore;
using Sedgewright.Entities;

namespace Sedgewright.Infras.Storage;

/// <summary>
/// EF Core context for the Sedgewright application data store.
/// </summary>
public sealed class SedgewrightDbContext(DbContextOptions<SedgewrightDbContext> options) : DbContext(options)
{
    public DbSet<AgentDefinition> AgentDefinitions => Set<AgentDefinition>();

    public DbSet<HarnessAgentDefinition> HarnessAgents => Set<HarnessAgentDefinition>();

    public DbSet<AgentSessionRecord> AgentSessions => Set<AgentSessionRecord>();

    public DbSet<AgentTool> AgentTools => Set<AgentTool>();

    public DbSet<ToolDefinition> ToolDefinitions => Set<ToolDefinition>();

    public DbSet<ConcurrentOrchestration> ConcurrentOrchestrations => Set<ConcurrentOrchestration>();

    public DbSet<ConcurrentOrchestrationParticipant> ConcurrentOrchestrationParticipants => Set<ConcurrentOrchestrationParticipant>();

    public DbSet<SequentialOrchestration> SequentialOrchestrations => Set<SequentialOrchestration>();

    public DbSet<SequentialOrchestrationAgent> SequentialOrchestrationAgents => Set<SequentialOrchestrationAgent>();

    public DbSet<GroupChatOrchestration> GroupChatOrchestrations => Set<GroupChatOrchestration>();

    public DbSet<GroupChatOrchestrationParticipant> GroupChatOrchestrationParticipants => Set<GroupChatOrchestrationParticipant>();

    public DbSet<MagenticOrchestration> MagenticOrchestrations => Set<MagenticOrchestration>();

    public DbSet<MagenticOrchestrationParticipant> MagenticOrchestrationParticipants => Set<MagenticOrchestrationParticipant>();

    public DbSet<HandoffOrchestration> HandoffOrchestrations => Set<HandoffOrchestration>();

    public DbSet<HandoffOrchestrationParticipant> HandoffOrchestrationParticipants => Set<HandoffOrchestrationParticipant>();

    public DbSet<HandoffOrchestrationRoute> HandoffOrchestrationRoutes => Set<HandoffOrchestrationRoute>();

    public DbSet<AgentSkillDefinition> AgentSkills => Set<AgentSkillDefinition>();

    public DbSet<AgentMCPAttachment> AgentMCPAttachments => Set<AgentMCPAttachment>();

    public DbSet<McpDefinition> MCPDefinitions => Set<McpDefinition>();

    public DbSet<LLMProvider> LLMProviders => Set<LLMProvider>();

    public DbSet<ApplicationSettings> ApplicationSettings => Set<ApplicationSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SedgewrightDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
