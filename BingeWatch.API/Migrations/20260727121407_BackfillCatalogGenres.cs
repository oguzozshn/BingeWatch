using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BingeOn.API.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCatalogGenres : Migration
    {
        /// <summary>
        /// Tür ve platform, katalog satırları yazıldıktan sonra eklendi; mevcut
        /// dizilerde bu alanlar boş. LastSyncedAt sıfırlanınca hepsi bayat sayılır
        /// ve ilk erişimde (ya da arka plan senkronunda) TMDb'den doldurulur.
        ///
        /// Şema değişmediği için Down() boş: geri alınacak bir şey yok, yalnızca
        /// bir kez fazladan senkron olur.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Shows SET LastSyncedAt = '0001-01-01T00:00:00'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
