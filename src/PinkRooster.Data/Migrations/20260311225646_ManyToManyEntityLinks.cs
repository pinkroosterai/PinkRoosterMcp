using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PinkRooster.Data.Migrations
{
    /// <inheritdoc />
    public partial class ManyToManyEntityLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create join tables first (before dropping old columns)
            migrationBuilder.CreateTable(
                name: "work_package_feature_request_links",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_package_id = table.Column<long>(type: "bigint", nullable: false),
                    feature_request_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_package_feature_request_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_package_feature_request_links_feature_requests_feature~",
                        column: x => x.feature_request_id,
                        principalTable: "feature_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_package_feature_request_links_work_packages_work_packa~",
                        column: x => x.work_package_id,
                        principalTable: "work_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_package_issue_links",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_package_id = table.Column<long>(type: "bigint", nullable: false),
                    issue_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_package_issue_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_package_issue_links_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_package_issue_links_work_packages_work_package_id",
                        column: x => x.work_package_id,
                        principalTable: "work_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_package_feature_request_links_feature_request_id",
                table: "work_package_feature_request_links",
                column: "feature_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_package_feature_request_links_work_package_id_feature_~",
                table: "work_package_feature_request_links",
                columns: new[] { "work_package_id", "feature_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_package_issue_links_issue_id",
                table: "work_package_issue_links",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_package_issue_links_work_package_id_issue_id",
                table: "work_package_issue_links",
                columns: new[] { "work_package_id", "issue_id" },
                unique: true);

            // 2. Migrate existing data from old FK columns to join tables
            migrationBuilder.Sql("""
                INSERT INTO work_package_issue_links (work_package_id, issue_id)
                SELECT id, linked_issue_id FROM work_packages
                WHERE linked_issue_id IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO work_package_feature_request_links (work_package_id, feature_request_id)
                SELECT id, linked_feature_request_id FROM work_packages
                WHERE linked_feature_request_id IS NOT NULL;
                """);

            // 3. Drop old FK columns (data has been migrated)
            migrationBuilder.DropForeignKey(
                name: "FK_work_packages_feature_requests_linked_feature_request_id",
                table: "work_packages");

            migrationBuilder.DropForeignKey(
                name: "FK_work_packages_issues_linked_issue_id",
                table: "work_packages");

            migrationBuilder.DropIndex(
                name: "IX_work_packages_linked_feature_request_id",
                table: "work_packages");

            migrationBuilder.DropIndex(
                name: "IX_work_packages_linked_issue_id",
                table: "work_packages");

            migrationBuilder.DropColumn(
                name: "linked_feature_request_id",
                table: "work_packages");

            migrationBuilder.DropColumn(
                name: "linked_issue_id",
                table: "work_packages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_package_feature_request_links");

            migrationBuilder.DropTable(
                name: "work_package_issue_links");

            migrationBuilder.AddColumn<long>(
                name: "linked_feature_request_id",
                table: "work_packages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "linked_issue_id",
                table: "work_packages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_linked_feature_request_id",
                table: "work_packages",
                column: "linked_feature_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_packages_linked_issue_id",
                table: "work_packages",
                column: "linked_issue_id");

            migrationBuilder.AddForeignKey(
                name: "FK_work_packages_feature_requests_linked_feature_request_id",
                table: "work_packages",
                column: "linked_feature_request_id",
                principalTable: "feature_requests",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_packages_issues_linked_issue_id",
                table: "work_packages",
                column: "linked_issue_id",
                principalTable: "issues",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
