using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YouTubester.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Video_Listing_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Videos_PublishedAt_VideoId",
                table: "Videos",
                columns: new[] { "PublishedAt", "VideoId" });

            // Case-insensitive title index for filtering performance
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_Videos_Title_NOCASE ON Videos(Title COLLATE NOCASE);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Videos_PublishedAt_VideoId",
                table: "Videos");

            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Videos_Title_NOCASE;");
        }
    }
}
