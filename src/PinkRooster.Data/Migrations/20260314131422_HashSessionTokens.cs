using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PinkRooster.Data.Migrations
{
    /// <inheritdoc />
    public partial class HashSessionTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear all existing sessions — they contain plaintext tokens that won't match hashed lookups
            migrationBuilder.Sql("DELETE FROM user_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
