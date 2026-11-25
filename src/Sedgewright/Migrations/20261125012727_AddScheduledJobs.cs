using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sedgewright.Migrations;

/// <inheritdoc />
public partial class _20261125012727_AddScheduledJobs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ScheduledJobs",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                TargetType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TargetId = table.Column<int>(type: "INTEGER", nullable: false),
                Prompt = table.Column<string>(type: "TEXT", nullable: false),
                ScheduleKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                IntervalSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                CronExpression = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                RunAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                StartAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                EndAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastRunAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastRunStatus = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScheduledJobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ScheduledJobRuns",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ScheduledJobId = table.Column<int>(type: "INTEGER", nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Output = table.Column<string>(type: "TEXT", nullable: true),
                Error = table.Column<string>(type: "TEXT", nullable: true),
                AgentSessionRecordId = table.Column<int>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScheduledJobRuns", x => x.Id);
                table.ForeignKey(
                    name: "FK_ScheduledJobRuns_AgentSessions_AgentSessionRecordId",
                    column: x => x.AgentSessionRecordId,
                    principalTable: "AgentSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_ScheduledJobRuns_ScheduledJobs_ScheduledJobId",
                    column: x => x.ScheduledJobId,
                    principalTable: "ScheduledJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ScheduledJobRuns_AgentSessionRecordId",
            table: "ScheduledJobRuns",
            column: "AgentSessionRecordId");

        migrationBuilder.CreateIndex(
            name: "IX_ScheduledJobRuns_ScheduledJobId_StartedAtUtc",
            table: "ScheduledJobRuns",
            columns: new[] { "ScheduledJobId", "StartedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ScheduledJobRuns_Status",
            table: "ScheduledJobRuns",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_ScheduledJobs_IsEnabled",
            table: "ScheduledJobs",
            column: "IsEnabled");

        migrationBuilder.CreateIndex(
            name: "IX_ScheduledJobs_TargetType_TargetId",
            table: "ScheduledJobs",
            columns: new[] { "TargetType", "TargetId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ScheduledJobRuns");

        migrationBuilder.DropTable(
            name: "ScheduledJobs");
    }
}
