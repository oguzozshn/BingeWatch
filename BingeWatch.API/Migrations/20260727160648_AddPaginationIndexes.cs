using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BingeOn.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPaginationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_Status_CreatedAt",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_ActivityEvents_UserId_CreatedAt",
                table: "ActivityEvents");

            migrationBuilder.CreateIndex(
                name: "IX_UserShows_UserId_Status",
                table: "UserShows",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLists_UpdatedAt_Id",
                table: "UserLists",
                columns: new[] { "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CreatedAt_Id",
                table: "Reviews",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status_CreatedAt_Id",
                table: "Reports",
                columns: new[] { "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt_Id",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_UserId_CreatedAt_Id",
                table: "ActivityEvents",
                columns: new[] { "UserId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserShows_UserId_Status",
                table: "UserShows");

            migrationBuilder.DropIndex(
                name: "IX_UserLists_UpdatedAt_Id",
                table: "UserLists");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_CreatedAt_Id",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reports_Status_CreatedAt_Id",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_CreatedAt_Id",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_ActivityEvents_UserId_CreatedAt_Id",
                table: "ActivityEvents");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status_CreatedAt",
                table: "Reports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_UserId_CreatedAt",
                table: "ActivityEvents",
                columns: new[] { "UserId", "CreatedAt" });
        }
    }
}
