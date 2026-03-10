using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PinkRooster.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_packages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_package_number = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    linked_issue_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    plan = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    estimated_complexity = table.Column<int>(type: "integer", nullable: true),
                    estimation_rationale = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    previous_active_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    attachments = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_packages", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_packages_issues_linked_issue_id",
                        column: x => x.linked_issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_packages_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_package_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_package_id = table.Column<long>(type: "bigint", nullable: false),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_package_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_package_audit_logs_work_packages_work_package_id",
                        column: x => x.work_package_id,
                        principalTable: "work_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_package_dependencies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dependent_work_package_id = table.Column<long>(type: "bigint", nullable: false),
                    depends_on_work_package_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_package_dependencies", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_package_dependencies_work_packages_dependent_work_pack~",
                        column: x => x.dependent_work_package_id,
                        principalTable: "work_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_package_dependencies_work_packages_depends_on_work_pac~",
                        column: x => x.depends_on_work_package_id,
                        principalTable: "work_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_package_phases",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    phase_number = table.Column<int>(type: "integer", nullable: false),
                    work_package_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_package_phases", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_package_phases_work_packages_work_package_id",
                        column: x => x.work_package_id,
                        principalTable: "work_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "acceptance_criteria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    phase_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    verification_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    verification_result = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acceptance_criteria", x => x.id);
                    table.ForeignKey(
                        name: "FK_acceptance_criteria_work_package_phases_phase_id",
                        column: x => x.phase_id,
                        principalTable: "work_package_phases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "phase_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    phase_id = table.Column<long>(type: "bigint", nullable: false),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_phase_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_phase_audit_logs_work_package_phases_phase_id",
                        column: x => x.phase_id,
                        principalTable: "work_package_phases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_package_tasks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_number = table.Column<int>(type: "integer", nullable: false),
                    phase_id = table.Column<long>(type: "bigint", nullable: false),
                    work_package_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    implementation_notes = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    previous_active_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    attachments = table.Column<string>(type: "jsonb", nullable: true),
                    target_files = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_package_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_package_tasks_work_package_phases_phase_id",
                        column: x => x.phase_id,
                        principalTable: "work_package_phases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_package_tasks_work_packages_work_package_id",
                        column: x => x.work_package_id,
                        principalTable: "work_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_id = table.Column<long>(type: "bigint", nullable: false),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_audit_logs_work_package_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "work_package_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_package_task_dependencies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dependent_task_id = table.Column<long>(type: "bigint", nullable: false),
                    depends_on_task_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_package_task_dependencies", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_package_task_dependencies_work_package_tasks_dependent~",
                        column: x => x.dependent_task_id,
                        principalTable: "work_package_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_package_task_dependencies_work_package_tasks_depends_o~",
                        column: x => x.depends_on_task_id,
                        principalTable: "work_package_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_acceptance_criteria_phase_id",
                table: "acceptance_criteria",
                column: "phase_id");

            migrationBuilder.CreateIndex(
                name: "IX_phase_audit_logs_phase_id",
                table: "phase_audit_logs",
                column: "phase_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_audit_logs_task_id",
                table: "task_audit_logs",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_package_audit_logs_work_package_id",
                table: "work_package_audit_logs",
                column: "work_package_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_package_dependencies_dependent_work_package_id_depends~",
                table: "work_package_dependencies",
                columns: new[] { "dependent_work_package_id", "depends_on_work_package_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_package_dependencies_depends_on_work_package_id",
                table: "work_package_dependencies",
                column: "depends_on_work_package_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_package_phases_work_package_id",
                table: "work_package_phases",
                column: "work_package_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_package_phases_work_package_id_phase_number",
                table: "work_package_phases",
                columns: new[] { "work_package_id", "phase_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_package_task_dependencies_dependent_task_id_depends_on~",
                table: "work_package_task_dependencies",
                columns: new[] { "dependent_task_id", "depends_on_task_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_package_task_dependencies_depends_on_task_id",
                table: "work_package_task_dependencies",
                column: "depends_on_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_package_tasks_phase_id",
                table: "work_package_tasks",
                column: "phase_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_package_tasks_state",
                table: "work_package_tasks",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "IX_work_package_tasks_work_package_id",
                table: "work_package_tasks",
                column: "work_package_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_package_tasks_work_package_id_task_number",
                table: "work_package_tasks",
                columns: new[] { "work_package_id", "task_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_linked_issue_id",
                table: "work_packages",
                column: "linked_issue_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_priority",
                table: "work_packages",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_project_id",
                table: "work_packages",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_project_id_work_package_number",
                table: "work_packages",
                columns: new[] { "project_id", "work_package_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_state",
                table: "work_packages",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_type",
                table: "work_packages",
                column: "type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acceptance_criteria");

            migrationBuilder.DropTable(
                name: "phase_audit_logs");

            migrationBuilder.DropTable(
                name: "task_audit_logs");

            migrationBuilder.DropTable(
                name: "work_package_audit_logs");

            migrationBuilder.DropTable(
                name: "work_package_dependencies");

            migrationBuilder.DropTable(
                name: "work_package_task_dependencies");

            migrationBuilder.DropTable(
                name: "work_package_tasks");

            migrationBuilder.DropTable(
                name: "work_package_phases");

            migrationBuilder.DropTable(
                name: "work_packages");
        }
    }
}
