using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BingeOn.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DailyActiveUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyActiveUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyActiveUsers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyTrafficStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Requests = table.Column<long>(type: "bigint", nullable: false),
                    ResponseBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyTrafficStats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyActiveUsers_Day_UserId",
                table: "DailyActiveUsers",
                columns: new[] { "Day", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyActiveUsers_UserId",
                table: "DailyActiveUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyTrafficStats_Day",
                table: "DailyTrafficStats",
                column: "Day",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyActiveUsers");

            migrationBuilder.DropTable(
                name: "DailyTrafficStats");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "AspNetUsers");
        }
    }
}
