using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BingeOn.API.Migrations
{
    /// <inheritdoc />
    public partial class MyNewChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeriesId = table.Column<int>(type: "int", nullable: false),
                    SeriesName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Overview = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PosterPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FirstAirDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AddedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchListItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchListItems_SeriesId_UserId",
                table: "WatchListItems",
                columns: new[] { "SeriesId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchListItems");
        }
    }
}
