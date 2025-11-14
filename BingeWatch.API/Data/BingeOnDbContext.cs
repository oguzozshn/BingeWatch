using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Models;

namespace BingeWatch.API.Data
{
    public class BingeOnDbContext : DbContext
    {
        public BingeOnDbContext(DbContextOptions<BingeOnDbContext> options) : base(options)
        {
        }

        public DbSet<WatchListItem> WatchListItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // WatchListItem konfigürasyonu
            modelBuilder.Entity<WatchListItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SeriesId).IsRequired();
                entity.Property(e => e.SeriesName).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Overview).HasMaxLength(2000);
                entity.Property(e => e.PosterPath).HasMaxLength(500);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
                
                // SeriesId ve UserId kombinasyonu unique olmalı
                entity.HasIndex(e => new { e.SeriesId, e.UserId }).IsUnique();
            });
        }
    }
} 