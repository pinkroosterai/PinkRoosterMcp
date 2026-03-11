using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PinkRooster.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStoriesCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "user_story",
                table: "feature_requests");

            migrationBuilder.AddColumn<string>(
                name: "user_stories",
                table: "feature_requests",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "user_stories",
                table: "feature_requests");

            migrationBuilder.AddColumn<string>(
                name: "user_story",
                table: "feature_requests",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }
    }
}
