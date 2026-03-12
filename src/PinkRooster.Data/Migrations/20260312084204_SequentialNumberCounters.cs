using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PinkRooster.Data.Migrations
{
    /// <inheritdoc />
    public partial class SequentialNumberCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "next_phase_number",
                table: "work_packages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "next_task_number",
                table: "work_packages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "next_fr_number",
                table: "projects",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "next_issue_number",
                table: "projects",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "next_wp_number",
                table: "projects",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Seed counters from current MAX values so existing data is respected
            migrationBuilder.Sql("""
                UPDATE projects SET next_issue_number = COALESCE(
                    (SELECT MAX(issue_number) + 1 FROM issues WHERE project_id = projects.id), 1);
                """);

            migrationBuilder.Sql("""
                UPDATE projects SET next_fr_number = COALESCE(
                    (SELECT MAX(feature_request_number) + 1 FROM feature_requests WHERE project_id = projects.id), 1);
                """);

            migrationBuilder.Sql("""
                UPDATE projects SET next_wp_number = COALESCE(
                    (SELECT MAX(work_package_number) + 1 FROM work_packages WHERE project_id = projects.id), 1);
                """);

            migrationBuilder.Sql("""
                UPDATE work_packages SET next_phase_number = COALESCE(
                    (SELECT MAX(phase_number) + 1 FROM work_package_phases WHERE work_package_id = work_packages.id), 1);
                """);

            migrationBuilder.Sql("""
                UPDATE work_packages SET next_task_number = COALESCE(
                    (SELECT MAX(task_number) + 1 FROM work_package_tasks WHERE work_package_id = work_packages.id), 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "next_phase_number",
                table: "work_packages");

            migrationBuilder.DropColumn(
                name: "next_task_number",
                table: "work_packages");

            migrationBuilder.DropColumn(
                name: "next_fr_number",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "next_issue_number",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "next_wp_number",
                table: "projects");
        }
    }
}
