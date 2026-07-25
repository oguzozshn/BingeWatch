using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BingeOn.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogAndEpisodeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOT: WatchListItems'ın DropTable'ı, EF'in ürettiği yerden (en başından)
            // alınıp Up()'ın sonuna taşındı; önce yeni tablolar oluşturulup veri
            // aktarılıyor, tablo ancak ondan sonra düşürülüyor.

            migrationBuilder.CreateTable(
                name: "Shows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TmdbId = table.Column<int>(type: "int", nullable: false),
                    ImdbId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Overview = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PosterPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BackdropPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FirstAirDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TmdbStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VoteAverage = table.Column<double>(type: "float", nullable: false),
                    VoteCount = table.Column<int>(type: "int", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShowId = table.Column<int>(type: "int", nullable: false),
                    SeasonNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Overview = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PosterPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AirDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EpisodeCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seasons_Shows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "Shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserShows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ShowId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsFavorite = table.Column<bool>(type: "bit", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserShows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserShows_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserShows_Shows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "Shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Episodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonId = table.Column<int>(type: "int", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Overview = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StillPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AirDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Runtime = table.Column<int>(type: "int", nullable: true),
                    TmdbVoteAverage = table.Column<double>(type: "float", nullable: false),
                    TmdbVoteCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Episodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Episodes_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchedEpisodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EpisodeId = table.Column<int>(type: "int", nullable: false),
                    WatchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RewatchNo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchedEpisodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchedEpisodes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchedEpisodes_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_AirDate",
                table: "Episodes",
                column: "AirDate");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_SeasonId_EpisodeNumber",
                table: "Episodes",
                columns: new[] { "SeasonId", "EpisodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_ShowId_SeasonNumber",
                table: "Seasons",
                columns: new[] { "ShowId", "SeasonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shows_ImdbId",
                table: "Shows",
                column: "ImdbId");

            migrationBuilder.CreateIndex(
                name: "IX_Shows_TmdbId",
                table: "Shows",
                column: "TmdbId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserShows_ShowId",
                table: "UserShows",
                column: "ShowId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShows_UserId_ShowId",
                table: "UserShows",
                columns: new[] { "UserId", "ShowId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchedEpisodes_EpisodeId",
                table: "WatchedEpisodes",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchedEpisodes_UserId_EpisodeId_RewatchNo",
                table: "WatchedEpisodes",
                columns: new[] { "UserId", "EpisodeId", "RewatchNo" },
                unique: true);

            // ---------------------------------------------------------------
            // Veri taşıma: WatchListItems -> Shows + UserShows
            //
            // Eski tabloda dizi bilgisi her kullanıcı için kopyalanıyordu; burada
            // dizi başına tek katalog satırı üretilip kullanıcı bağı ayrılıyor.
            // LastSyncedAt bilerek '0001-01-01' bırakılıyor: satır bir taslak,
            // katalog servisi ilk erişimde TMDb'den sezon/bölüm ile zenginleştirir.
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
                INSERT INTO [Shows] ([TmdbId], [Name], [Overview], [PosterPath], [FirstAirDate], [VoteAverage], [VoteCount], [LastSyncedAt])
                SELECT  w.[SeriesId],
                        MAX(w.[SeriesName]),
                        MAX(w.[Overview]),
                        MAX(w.[PosterPath]),
                        MAX(w.[FirstAirDate]),
                        0, 0, '0001-01-01T00:00:00'
                FROM    [WatchListItems] w
                WHERE   NOT EXISTS (SELECT 1 FROM [Shows] s WHERE s.[TmdbId] = w.[SeriesId])
                        -- Sahipsiz dizileri katalogla taşıma: kimlik doğrulama
                        -- öncesinden kalan satırların dizileri de çöp olabilir.
                        AND EXISTS (SELECT 1 FROM [AspNetUsers] u WHERE u.[Id] = w.[UserId])
                GROUP BY w.[SeriesId];");

            // Yalnızca gerçek bir hesaba bağlı satırlar taşınır. Kimlik doğrulama
            // öncesinden kalan sahte kullanıcı ("user1") satırları AspNetUsers'da
            // karşılığı olmadığı için burada elenir — FK'yi ihlal ederlerdi.
            migrationBuilder.Sql(@"
                INSERT INTO [UserShows] ([UserId], [ShowId], [Status], [IsFavorite], [AddedAt])
                SELECT  w.[UserId], s.[Id], 0, 0, MIN(w.[AddedDate])
                FROM    [WatchListItems] w
                JOIN    [Shows] s        ON s.[TmdbId] = w.[SeriesId]
                JOIN    [AspNetUsers] u  ON u.[Id] = w.[UserId]
                GROUP BY w.[UserId], s.[Id];");

            migrationBuilder.DropTable(
                name: "WatchListItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserShows");

            migrationBuilder.DropTable(
                name: "WatchedEpisodes");

            migrationBuilder.DropTable(
                name: "Episodes");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropTable(
                name: "Shows");

            migrationBuilder.CreateTable(
                name: "WatchListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AddedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FirstAirDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Overview = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PosterPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SeriesId = table.Column<int>(type: "int", nullable: false),
                    SeriesName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
    }
}
