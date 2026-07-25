using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Models;

namespace BingeWatch.API.Data
{
    public class BingeOnDbContext : IdentityDbContext<AppUser>
    {
        public BingeOnDbContext(DbContextOptions<BingeOnDbContext> options) : base(options)
        {
        }

        // Katalog — TMDb'nin yerel kopyası
        public DbSet<Show> Shows { get; set; }
        public DbSet<Season> Seasons { get; set; }
        public DbSet<Episode> Episodes { get; set; }

        // Kullanıcı katmanı
        public DbSet<UserShow> UserShows { get; set; }
        public DbSet<WatchedEpisode> WatchedEpisodes { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<Review> Reviews { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Show>(entity =>
            {
                entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Overview).HasMaxLength(4000);
                entity.Property(e => e.PosterPath).HasMaxLength(500);
                entity.Property(e => e.BackdropPath).HasMaxLength(500);
                entity.Property(e => e.ImdbId).HasMaxLength(50);
                entity.Property(e => e.TmdbStatus).HasMaxLength(100);

                entity.HasIndex(e => e.TmdbId).IsUnique();
                entity.HasIndex(e => e.ImdbId);

                entity.HasMany(e => e.Seasons)
                      .WithOne(s => s.Show)
                      .HasForeignKey(s => s.ShowId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Season>(entity =>
            {
                entity.Property(e => e.Name).HasMaxLength(500);
                entity.Property(e => e.Overview).HasMaxLength(4000);
                entity.Property(e => e.PosterPath).HasMaxLength(500);

                entity.HasIndex(e => new { e.ShowId, e.SeasonNumber }).IsUnique();

                entity.HasMany(e => e.Episodes)
                      .WithOne(ep => ep.Season)
                      .HasForeignKey(ep => ep.SeasonId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Episode>(entity =>
            {
                entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Overview).HasMaxLength(4000);
                entity.Property(e => e.StillPath).HasMaxLength(500);

                entity.HasIndex(e => new { e.SeasonId, e.EpisodeNumber }).IsUnique();
                // Takvim/"yaklaşan bölümler" sorguları bu kolona göre tarar
                entity.HasIndex(e => e.AirDate);
            });

            modelBuilder.Entity<UserShow>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);

                entity.HasIndex(e => new { e.UserId, e.ShowId }).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Show)
                      .WithMany()
                      .HasForeignKey(e => e.ShowId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WatchedEpisode>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);

                // Aynı bölümün aynı tur izlemesi tek satır olmalı
                entity.HasIndex(e => new { e.UserId, e.EpisodeId, e.RewatchNo }).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Episode)
                      .WithMany()
                      .HasForeignKey(e => e.EpisodeId)
                      // Episode silinince Cascade, AppUser tarafındaki cascade ile
                      // çoklu yol oluşturur; SQL Server buna izin vermez.
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Rating>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.Value).HasColumnType("decimal(2,1)");

                // Bir kullanıcı aynı hedefe yalnızca tek puan verebilir; ikinci puan güncellemedir.
                entity.HasIndex(e => new { e.UserId, e.TargetType, e.TargetId }).IsUnique();
                // Dizi sayfasındaki ortalama/histogram bu yöne göre tarar.
                entity.HasIndex(e => new { e.TargetType, e.TargetId });

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // TargetId polimorfik olduğu için FK yok; hedefin varlığını servis doğrular.
            });

            modelBuilder.Entity<Review>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.Body).IsRequired().HasMaxLength(10000);

                // Kullanıcı başına dizi/sezon hedefinde tek inceleme; tekrar yazmak düzenlemedir.
                // HasFilter(null): EF'in varsayılan "IS NOT NULL" filtresi dizi geneli
                // incelemeleri (SeasonNumber = NULL) tekillik dışında bırakırdı.
                entity.HasIndex(e => new { e.UserId, e.ShowId, e.SeasonNumber }).IsUnique().HasFilter(null);
                entity.HasIndex(e => new { e.ShowId, e.CreatedAt });

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Show)
                      .WithMany()
                      .HasForeignKey(e => e.ShowId)
                      // AppUser tarafındaki cascade ile çoklu yol oluşur; SQL Server izin vermez.
                      .OnDelete(DeleteBehavior.NoAction);
            });
        }
    }
}
