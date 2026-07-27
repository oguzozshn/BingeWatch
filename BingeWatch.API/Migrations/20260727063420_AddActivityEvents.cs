using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BingeOn.API.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShowId = table.Column<int>(type: "int", nullable: true),
                    SeasonNumber = table.Column<int>(type: "int", nullable: true),
                    EpisodeId = table.Column<int>(type: "int", nullable: true),
                    EpisodeCount = table.Column<int>(type: "int", nullable: true),
                    RatingValue = table.Column<decimal>(type: "decimal(2,1)", nullable: true),
                    ReviewId = table.Column<int>(type: "int", nullable: true),
                    TargetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityEvents_AspNetUsers_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActivityEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityEvents_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActivityEvents_Shows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "Shows",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_EpisodeId",
                table: "ActivityEvents",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_ShowId",
                table: "ActivityEvents",
                column: "ShowId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_TargetUserId",
                table: "ActivityEvents",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_UserId_CreatedAt",
                table: "ActivityEvents",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_UserId_Type_ShowId",
                table: "ActivityEvents",
                columns: new[] { "UserId", "Type", "ShowId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityEvents");
        }
    }
}
