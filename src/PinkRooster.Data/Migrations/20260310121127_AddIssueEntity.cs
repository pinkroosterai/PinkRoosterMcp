using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PinkRooster.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issues",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    issue_number = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    issue_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    steps_to_reproduce = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    expected_behavior = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    actual_behavior = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    affected_component = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    stack_trace = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    root_cause = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    attachments = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issues", x => x.id);
                    table.ForeignKey(
                        name: "FK_issues_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "issue_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    issue_id = table.Column<long>(type: "bigint", nullable: false),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issue_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_issue_audit_logs_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_issue_audit_logs_issue_id",
                table: "issue_audit_logs",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "IX_issues_priority",
                table: "issues",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "IX_issues_project_id",
                table: "issues",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_issues_project_id_issue_number",
                table: "issues",
                columns: new[] { "project_id", "issue_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_issues_severity",
                table: "issues",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "IX_issues_state",
                table: "issues",
                column: "state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_audit_logs");

            migrationBuilder.DropTable(
                name: "issues");
        }
    }
}
