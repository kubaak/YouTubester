using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YouTubester.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddETagAndCommentsAllowed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CommentsAllowed",
                table: "Videos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ETag",
                table: "Videos",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ETag",
                table: "Playlists",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ETag",
                table: "Channels",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommentsAllowed",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "ETag",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "ETag",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "ETag",
                table: "Channels");
        }
    }
}
