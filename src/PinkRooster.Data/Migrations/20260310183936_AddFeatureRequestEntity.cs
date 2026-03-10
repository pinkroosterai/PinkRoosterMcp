using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PinkRooster.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureRequestEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "linked_feature_request_id",
                table: "work_packages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "feature_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    feature_request_number = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    business_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    user_story = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    requester = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    acceptance_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    attachments = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_feature_requests_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_request_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    feature_request_id = table.Column<long>(type: "bigint", nullable: false),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_request_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_feature_request_audit_logs_feature_requests_feature_request~",
                        column: x => x.feature_request_id,
                        principalTable: "feature_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_linked_feature_request_id",
                table: "work_packages",
                column: "linked_feature_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_request_audit_logs_feature_request_id",
                table: "feature_request_audit_logs",
                column: "feature_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_requests_category",
                table: "feature_requests",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_feature_requests_priority",
                table: "feature_requests",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "IX_feature_requests_project_id",
                table: "feature_requests",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_requests_project_id_feature_request_number",
                table: "feature_requests",
                columns: new[] { "project_id", "feature_request_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feature_requests_status",
                table: "feature_requests",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "FK_work_packages_feature_requests_linked_feature_request_id",
                table: "work_packages",
                column: "linked_feature_request_id",
                principalTable: "feature_requests",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_packages_feature_requests_linked_feature_request_id",
                table: "work_packages");

            migrationBuilder.DropTable(
                name: "feature_request_audit_logs");

            migrationBuilder.DropTable(
                name: "feature_requests");

            migrationBuilder.DropIndex(
                name: "IX_work_packages_linked_feature_request_id",
                table: "work_packages");

            migrationBuilder.DropColumn(
                name: "linked_feature_request_id",
                table: "work_packages");
        }
    }
}
