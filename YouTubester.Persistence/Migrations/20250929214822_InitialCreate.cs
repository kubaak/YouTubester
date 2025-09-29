using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YouTubester.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Replies",
                columns: table => new
                {
                    CommentId = table.Column<string>(type: "TEXT", nullable: false),
                    VideoId = table.Column<string>(type: "TEXT", nullable: false),
                    VideoTitle = table.Column<string>(type: "TEXT", nullable: false),
                    CommentText = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SuggestedText = table.Column<string>(type: "TEXT", nullable: true),
                    FinalText = table.Column<string>(type: "TEXT", nullable: true),
                    PulledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SuggestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Replies", x => x.CommentId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Replies_VideoId",
                table: "Replies",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Replies");
        }
    }
}
