using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YouTubester.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VideoVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsPublic",
                table: "Videos",
                newName: "Visibility");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Visibility",
                table: "Videos",
                newName: "IsPublic");
        }
    }
}
