using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sedgewright.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultWorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    ThemeMode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    LastChatTargetType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    LastChatTargetId = table.Column<int>(type: "INTEGER", nullable: true),
                    LastAgentSessionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConcurrentOrchestrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcurrentOrchestrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupChatOrchestrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    MaximumIterationCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupChatOrchestrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LLMProviders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderKind = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    EnabledModelsJson = table.Column<string>(type: "TEXT", maxLength: 32768, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LLMProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MCPDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsStdio = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MCPDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SequentialOrchestrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ChainOnlyAgentResponses = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SequentialOrchestrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    SystemPrompt = table.Column<string>(type: "TEXT", nullable: true),
                    UserPrompt = table.Column<string>(type: "TEXT", nullable: true),
                    IsReadOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ShellExecutor = table.Column<string>(type: "TEXT", nullable: false),
                    ShellWorkingDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    ShellIsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LLMProviderId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentDefinitions_LLMProviders_LLMProviderId",
                        column: x => x.LLMProviderId,
                        principalTable: "LLMProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentMCPAttachments",
                columns: table => new
                {
                    AgentId = table.Column<int>(type: "INTEGER", nullable: false),
                    McpId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMCPAttachments", x => new { x.AgentId, x.McpId });
                    table.ForeignKey(
                        name: "FK_AgentMCPAttachments_AgentDefinitions_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentMCPAttachments_MCPDefinitions_McpId",
                        column: x => x.McpId,
                        principalTable: "MCPDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgentDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    SerializedSessionJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentSessions_AgentDefinitions_AgentDefinitionId",
                        column: x => x.AgentDefinitionId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentSkills",
                columns: table => new
                {
                    AgentId = table.Column<int>(type: "INTEGER", nullable: false),
                    SkillDirectory = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    SelectedSkills = table.Column<string>(type: "TEXT", maxLength: 32768, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSkills", x => new { x.AgentId, x.SkillDirectory });
                    table.ForeignKey(
                        name: "FK_AgentSkills_AgentDefinitions_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConcurrentOrchestrationParticipants",
                columns: table => new
                {
                    ConcurrentOrchestrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentDefinitionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcurrentOrchestrationParticipants", x => new { x.ConcurrentOrchestrationId, x.AgentDefinitionId });
                    table.ForeignKey(
                        name: "FK_ConcurrentOrchestrationParticipants_AgentDefinitions_AgentDefinitionId",
                        column: x => x.AgentDefinitionId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConcurrentOrchestrationParticipants_ConcurrentOrchestrations_ConcurrentOrchestrationId",
                        column: x => x.ConcurrentOrchestrationId,
                        principalTable: "ConcurrentOrchestrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupChatOrchestrationParticipants",
                columns: table => new
                {
                    GroupChatOrchestrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupChatOrchestrationParticipants", x => new { x.GroupChatOrchestrationId, x.AgentDefinitionId });
                    table.ForeignKey(
                        name: "FK_GroupChatOrchestrationParticipants_AgentDefinitions_AgentDefinitionId",
                        column: x => x.AgentDefinitionId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupChatOrchestrationParticipants_GroupChatOrchestrations_GroupChatOrchestrationId",
                        column: x => x.GroupChatOrchestrationId,
                        principalTable: "GroupChatOrchestrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HandoffOrchestrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    StartAgentDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsAutonomousModeEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoffOrchestrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoffOrchestrations_AgentDefinitions_StartAgentDefinitionId",
                        column: x => x.StartAgentDefinitionId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HarnessAgentDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    DisableTodoProvider = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisableAgentModeProvider = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisableFileMemory = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisableWebSearch = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisableToolAutoApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisableAgentSkillsProvider = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisableOpenTelemetry = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxContextWindowTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxOutputTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    FileAccessWorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EnableCodeExecution = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HarnessAgentDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HarnessAgentDefinitions_AgentDefinitions_Id",
                        column: x => x.Id,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MagenticOrchestrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ManagerAgentDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequirePlanSignoff = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    MaximumRoundCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 10),
                    MaximumStallCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 3),
                    MaximumResetCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 2)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MagenticOrchestrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MagenticOrchestrations_AgentDefinitions_ManagerAgentDefinitionId",
                        column: x => x.ManagerAgentDefinitionId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SequentialOrchestrationAgents",
                columns: table => new
                {
                    SequentialOrchestrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SequentialOrchestrationAgents", x => new { x.SequentialOrchestrationId, x.AgentDefinitionId });
                    table.ForeignKey(
                        name: "FK_SequentialOrchestrationAgents_AgentDefinitions_AgentDefinitionId",
                        column: x => x.AgentDefinitionId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SequentialOrchestrationAgents_SequentialOrchestrations_SequentialOrchestrationId",
                        column: x => x.SequentialOrchestrationId,
                        principalTable: "SequentialOrchestrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HandoffOrchestrationParticipants",
                columns: table => new
                {
                    HandoffOrchestrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentDefinitionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoffOrchestrationParticipants", x => new { x.HandoffOrchestrationId, x.AgentDefinitionId });
                    table.ForeignKey(
                        name: "FK_HandoffOrchestrationParticipants_AgentDefinitions_AgentDefinitionId",
                        column: x => x.AgentDefinitionId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoffOrchestrationParticipants_HandoffOrchestrations_HandoffOrchestrationId",
                        column: x => x.HandoffOrchestrationId,
                        principalTable: "HandoffOrchestrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HandoffOrchestrationRoutes",
                columns: table => new
                {
                    HandoffOrchestrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceAgentDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetAgentDefinitionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoffOrchestrationRoutes", x => new { x.HandoffOrchestrationId, x.SourceAgentDefinitionId, x.TargetAgentDefinitionId });
                    table.ForeignKey(
                        name: "FK_HandoffOrchestrationRoutes_AgentDefinitions_SourceAgentDefinitionId",
                        column: x => x.SourceAgentDefinitionId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoffOrchestrationRoutes_AgentDefinitions_TargetAgentDefinitionId",
                        column: x => x.TargetAgentDefinitionId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoffOrchestrationRoutes_HandoffOrchestrations_HandoffOrchestrationId",
                        column: x => x.HandoffOrchestrationId,
                        principalTable: "HandoffOrchestrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MagenticOrchestrationParticipants",
                columns: table => new
                {
                    MagenticOrchestrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MagenticOrchestrationParticipants", x => new { x.MagenticOrchestrationId, x.AgentDefinitionId });
                    table.ForeignKey(
                        name: "FK_MagenticOrchestrationParticipants_AgentDefinitions_AgentDefinitionId",
                        column: x => x.AgentDefinitionId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MagenticOrchestrationParticipants_MagenticOrchestrations_MagenticOrchestrationId",
                        column: x => x.MagenticOrchestrationId,
                        principalTable: "MagenticOrchestrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToolDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ToolDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SequentialOrchestrationId = table.Column<int>(type: "INTEGER", nullable: true),
                    ConcurrentOrchestrationId = table.Column<int>(type: "INTEGER", nullable: true),
                    GroupChatOrchestrationId = table.Column<int>(type: "INTEGER", nullable: true),
                    MagenticOrchestrationId = table.Column<int>(type: "INTEGER", nullable: true),
                    HandoffOrchestrationId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolDefinitions_ConcurrentOrchestrations_ConcurrentOrchestrationId",
                        column: x => x.ConcurrentOrchestrationId,
                        principalTable: "ConcurrentOrchestrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolDefinitions_GroupChatOrchestrations_GroupChatOrchestrationId",
                        column: x => x.GroupChatOrchestrationId,
                        principalTable: "GroupChatOrchestrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolDefinitions_HandoffOrchestrations_HandoffOrchestrationId",
                        column: x => x.HandoffOrchestrationId,
                        principalTable: "HandoffOrchestrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolDefinitions_MagenticOrchestrations_MagenticOrchestrationId",
                        column: x => x.MagenticOrchestrationId,
                        principalTable: "MagenticOrchestrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolDefinitions_SequentialOrchestrations_SequentialOrchestrationId",
                        column: x => x.SequentialOrchestrationId,
                        principalTable: "SequentialOrchestrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentTools",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgentId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToolDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentTools_AgentDefinitions_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AgentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentTools_ToolDefinitions_ToolDefinitionId",
                        column: x => x.ToolDefinitionId,
                        principalTable: "ToolDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentDefinitions_LLMProviderId",
                table: "AgentDefinitions",
                column: "LLMProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentDefinitions_Name",
                table: "AgentDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentMCPAttachments_McpId",
                table: "AgentMCPAttachments",
                column: "McpId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_AgentDefinitionId",
                table: "AgentSessions",
                column: "AgentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_LastUsedAtUtc",
                table: "AgentSessions",
                column: "LastUsedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTools_AgentId",
                table: "AgentTools",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTools_AgentId_ToolDefinitionId",
                table: "AgentTools",
                columns: new[] { "AgentId", "ToolDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentTools_ToolDefinitionId",
                table: "AgentTools",
                column: "ToolDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcurrentOrchestrationParticipants_AgentDefinitionId",
                table: "ConcurrentOrchestrationParticipants",
                column: "AgentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcurrentOrchestrations_Name",
                table: "ConcurrentOrchestrations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupChatOrchestrationParticipants_AgentDefinitionId",
                table: "GroupChatOrchestrationParticipants",
                column: "AgentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupChatOrchestrationParticipants_GroupChatOrchestrationId_StepOrder",
                table: "GroupChatOrchestrationParticipants",
                columns: new[] { "GroupChatOrchestrationId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupChatOrchestrations_Name",
                table: "GroupChatOrchestrations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandoffOrchestrationParticipants_AgentDefinitionId",
                table: "HandoffOrchestrationParticipants",
                column: "AgentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoffOrchestrationRoutes_SourceAgentDefinitionId",
                table: "HandoffOrchestrationRoutes",
                column: "SourceAgentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoffOrchestrationRoutes_TargetAgentDefinitionId",
                table: "HandoffOrchestrationRoutes",
                column: "TargetAgentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoffOrchestrations_Name",
                table: "HandoffOrchestrations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandoffOrchestrations_StartAgentDefinitionId",
                table: "HandoffOrchestrations",
                column: "StartAgentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MagenticOrchestrationParticipants_AgentDefinitionId",
                table: "MagenticOrchestrationParticipants",
                column: "AgentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MagenticOrchestrationParticipants_MagenticOrchestrationId_StepOrder",
                table: "MagenticOrchestrationParticipants",
                columns: new[] { "MagenticOrchestrationId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MagenticOrchestrations_ManagerAgentDefinitionId",
                table: "MagenticOrchestrations",
                column: "ManagerAgentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MagenticOrchestrations_Name",
                table: "MagenticOrchestrations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MCPDefinitions_Name",
                table: "MCPDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SequentialOrchestrationAgents_AgentDefinitionId",
                table: "SequentialOrchestrationAgents",
                column: "AgentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_SequentialOrchestrationAgents_SequentialOrchestrationId_StepOrder",
                table: "SequentialOrchestrationAgents",
                columns: new[] { "SequentialOrchestrationId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SequentialOrchestrations_Name",
                table: "SequentialOrchestrations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_ConcurrentOrchestrationId",
                table: "ToolDefinitions",
                column: "ConcurrentOrchestrationId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_GroupChatOrchestrationId",
                table: "ToolDefinitions",
                column: "GroupChatOrchestrationId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_HandoffOrchestrationId",
                table: "ToolDefinitions",
                column: "HandoffOrchestrationId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_MagenticOrchestrationId",
                table: "ToolDefinitions",
                column: "MagenticOrchestrationId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_Name",
                table: "ToolDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_SequentialOrchestrationId",
                table: "ToolDefinitions",
                column: "SequentialOrchestrationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentMCPAttachments");

            migrationBuilder.DropTable(
                name: "AgentSessions");

            migrationBuilder.DropTable(
                name: "AgentSkills");

            migrationBuilder.DropTable(
                name: "AgentTools");

            migrationBuilder.DropTable(
                name: "ApplicationSettings");

            migrationBuilder.DropTable(
                name: "ConcurrentOrchestrationParticipants");

            migrationBuilder.DropTable(
                name: "GroupChatOrchestrationParticipants");

            migrationBuilder.DropTable(
                name: "HandoffOrchestrationParticipants");

            migrationBuilder.DropTable(
                name: "HandoffOrchestrationRoutes");

            migrationBuilder.DropTable(
                name: "HarnessAgentDefinitions");

            migrationBuilder.DropTable(
                name: "MagenticOrchestrationParticipants");

            migrationBuilder.DropTable(
                name: "SequentialOrchestrationAgents");

            migrationBuilder.DropTable(
                name: "MCPDefinitions");

            migrationBuilder.DropTable(
                name: "ToolDefinitions");

            migrationBuilder.DropTable(
                name: "ConcurrentOrchestrations");

            migrationBuilder.DropTable(
                name: "GroupChatOrchestrations");

            migrationBuilder.DropTable(
                name: "HandoffOrchestrations");

            migrationBuilder.DropTable(
                name: "MagenticOrchestrations");

            migrationBuilder.DropTable(
                name: "SequentialOrchestrations");

            migrationBuilder.DropTable(
                name: "AgentDefinitions");

            migrationBuilder.DropTable(
                name: "LLMProviders");
        }
    }
}
