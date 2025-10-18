using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YouTubester.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VideosDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CachedAt",
                table: "Videos",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "CategoryId",
                table: "Videos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultAudioLanguage",
                table: "Videos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultLanguage",
                table: "Videos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Videos",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                table: "Videos",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Videos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LocationDescription",
                table: "Videos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Location_Latitude",
                table: "Videos",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Location_Longitude",
                table: "Videos",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Videos",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Videos",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CachedAt",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "DefaultAudioLanguage",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "DefaultLanguage",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "LocationDescription",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Location_Latitude",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Location_Longitude",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Videos");
        }
    }
}
