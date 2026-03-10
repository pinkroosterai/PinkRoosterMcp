using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PinkRooster.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    http_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    path = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    caller_identity = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_caller_identity",
                table: "activity_logs",
                column: "caller_identity");

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_timestamp",
                table: "activity_logs",
                column: "timestamp",
                descending: new[] { true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_logs");
        }
    }
}
