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
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Network> Networks { get; set; }

        // Kullanıcı katmanı
        public DbSet<UserShow> UserShows { get; set; }
        public DbSet<WatchedEpisode> WatchedEpisodes { get; set; }
        public DbSet<EpisodeBookmark> EpisodeBookmarks { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<Review> Reviews { get; set; }

        // Sosyal katman
        public DbSet<Follow> Follows { get; set; }
        public DbSet<ActivityEvent> ActivityEvents { get; set; }
        public DbSet<ReviewLike> ReviewLikes { get; set; }
        public DbSet<ReviewComment> ReviewComments { get; set; }
        public DbSet<EpisodeComment> EpisodeComments { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // Listeler
        public DbSet<UserList> UserLists { get; set; }
        public DbSet<UserListItem> UserListItems { get; set; }
        public DbSet<UserListLike> UserListLikes { get; set; }

        // Moderasyon
        public DbSet<UserBlock> UserBlocks { get; set; }
        public DbSet<Report> Reports { get; set; }

        // İşletim metrikleri — admin panelindeki sayaçlar
        public DbSet<DailyTrafficStat> DailyTrafficStats { get; set; }
        public DbSet<DailyActiveUser> DailyActiveUsers { get; set; }

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

                // Tür ve platform çoka-çok; ara tabloları EF'in varsayılan
                // adlandırmasıyla (ShowGenre / NetworkShow) üretilir.
                entity.HasMany(e => e.Genres)
                      .WithMany(g => g.Shows);

                entity.HasMany(e => e.Networks)
                      .WithMany(n => n.Shows);
            });

            modelBuilder.Entity<Genre>(entity =>
            {
                // TMDb id'si birincil anahtar; EF'in kendi değer üretmesi engellenmeli.
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            });

            modelBuilder.Entity<Network>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.LogoPath).HasMaxLength(500);
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
                // "Sırada ne var" ve istatistikler kullanıcının dizilerini duruma
                // göre süzer; ana sayfa her açılışta bu sorguyu çalıştırıyor.
                entity.HasIndex(e => new { e.UserId, e.Status });

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

            modelBuilder.Entity<EpisodeBookmark>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);

                // "Kaldığım yer" bölüm başına tektir; ikinci kayıt güncellemedir.
                entity.HasIndex(e => new { e.UserId, e.EpisodeId }).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Episode)
                      .WithMany()
                      .HasForeignKey(e => e.EpisodeId)
                      // WatchedEpisode ile aynı sebep: çoklu cascade yolu.
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
                // Genel inceleme akışı dizi filtresi olmadan tarihe göre tarar;
                // imleç (CreatedAt, Id) çiftinden ilerlediği için ikisi birlikte.
                entity.HasIndex(e => new { e.CreatedAt, e.Id });

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

            modelBuilder.Entity<Follow>(entity =>
            {
                entity.Property(e => e.FollowerId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.FolloweeId).IsRequired().HasMaxLength(450);

                // Aynı çift iki kez takip edilemez; ikinci istek sessizce yoksayılır.
                entity.HasIndex(e => new { e.FollowerId, e.FolloweeId }).IsUnique();
                // Takipçi listesi ters yönden okunur.
                entity.HasIndex(e => new { e.FolloweeId, e.CreatedAt });

                entity.HasOne(e => e.Follower)
                      .WithMany()
                      .HasForeignKey(e => e.FollowerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Followee)
                      .WithMany()
                      .HasForeignKey(e => e.FolloweeId)
                      // Aynı tabloya ikinci cascade yolu; SQL Server izin vermez.
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ActivityEvent>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.TargetUserId).HasMaxLength(450);
                entity.Property(e => e.RatingValue).HasColumnType("decimal(2,1)");

                // Akış, takip edilenlerin olaylarını tarihe göre okur; imleç
                // (CreatedAt, Id) çiftinden ilerlediği için Id de indekste.
                entity.HasIndex(e => new { e.UserId, e.CreatedAt, e.Id });
                // Puan/inceleme güncellemesinde mevcut olayı bulmak için.
                entity.HasIndex(e => new { e.UserId, e.Type, e.ShowId });

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Aşağıdaki üç ilişki de AppUser cascade'i ile çoklu yol oluşturur.
                entity.HasOne(e => e.Show)
                      .WithMany()
                      .HasForeignKey(e => e.ShowId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Episode)
                      .WithMany()
                      .HasForeignKey(e => e.EpisodeId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.TargetUser)
                      .WithMany()
                      .HasForeignKey(e => e.TargetUserId)
                      .OnDelete(DeleteBehavior.NoAction);

                // ReviewId'ye FK verilmiyor: inceleme silinince olay da servis
                // tarafından siliniyor, ayrıca cascade yolu kalabalıklaşıyor.
            });

            modelBuilder.Entity<ReviewLike>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);

                // Bir kullanıcı aynı incelemeyi bir kez beğenir.
                entity.HasIndex(e => new { e.ReviewId, e.UserId }).IsUnique();

                entity.HasOne(e => e.Review)
                      .WithMany()
                      .HasForeignKey(e => e.ReviewId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      // Review zaten AppUser'a cascade veriyor; ikinci yol SQL Server'da yasak.
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ReviewComment>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.Body).IsRequired().HasMaxLength(2000);

                entity.HasIndex(e => new { e.ReviewId, e.CreatedAt });

                entity.HasOne(e => e.Review)
                      .WithMany()
                      .HasForeignKey(e => e.ReviewId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<EpisodeComment>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.Body).IsRequired().HasMaxLength(2000);

                // İplik tek bir bölümün altında, eskiden yeniye okunur.
                entity.HasIndex(e => new { e.EpisodeId, e.CreatedAt });

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Episode)
                      .WithMany()
                      .HasForeignKey(e => e.EpisodeId)
                      // AppUser tarafındaki cascade ile çoklu yol oluşur; SQL Server
                      // izin vermez (bkz. WatchedEpisode).
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.ActorId).IsRequired().HasMaxLength(450);

                // Zil rozeti okunmamışları sayar, liste tarihe göre okur
                // (imleç (CreatedAt, Id) çiftinden ilerliyor).
                entity.HasIndex(e => new { e.UserId, e.ReadAt });
                entity.HasIndex(e => new { e.UserId, e.CreatedAt, e.Id });

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Actor)
                      .WithMany()
                      .HasForeignKey(e => e.ActorId)
                      .OnDelete(DeleteBehavior.NoAction);

                // ReviewId'ye FK yok: inceleme silinince bildirimi de servis siliyor.
            });

            modelBuilder.Entity<UserList>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(2000);

                // Profildeki liste sekmesi kullanıcının listelerini tarihe göre okur.
                entity.HasIndex(e => new { e.UserId, e.UpdatedAt });
                // Keşif akışı kullanıcı filtresi olmadan UpdatedAt'e göre tarar;
                // imleç (UpdatedAt, Id) çiftinden ilerliyor.
                entity.HasIndex(e => new { e.UpdatedAt, e.Id });

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Items)
                      .WithOne(i => i.UserList)
                      .HasForeignKey(i => i.UserListId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserListItem>(entity =>
            {
                entity.Property(e => e.Note).HasMaxLength(1000);

                // Aynı dizi bir listeye iki kez eklenemez.
                entity.HasIndex(e => new { e.UserListId, e.ShowId }).IsUnique();
                // Detay sayfası öğeleri sıraya göre okur.
                entity.HasIndex(e => new { e.UserListId, e.Position });

                entity.HasOne(e => e.Show)
                      .WithMany()
                      .HasForeignKey(e => e.ShowId)
                      // UserList → AppUser cascade'i ile çoklu yol oluşur; SQL Server izin vermez.
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<UserListLike>(entity =>
            {
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);

                // Bir kullanıcı aynı listeyi bir kez beğenir.
                entity.HasIndex(e => new { e.UserListId, e.UserId }).IsUnique();

                entity.HasOne(e => e.UserList)
                      .WithMany()
                      .HasForeignKey(e => e.UserListId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      // UserList zaten AppUser'a cascade veriyor; ikinci yol SQL Server'da yasak.
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<UserBlock>(entity =>
            {
                entity.Property(e => e.BlockerId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.BlockedId).IsRequired().HasMaxLength(450);

                // Aynı çift iki kez engellenemez; ikinci istek sessizce yoksayılır.
                entity.HasIndex(e => new { e.BlockerId, e.BlockedId }).IsUnique();
                // Engel filtreleri "beni kim engelledi" yönünden de tarar.
                entity.HasIndex(e => e.BlockedId);

                entity.HasOne(e => e.Blocker)
                      .WithMany()
                      .HasForeignKey(e => e.BlockerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Blocked)
                      .WithMany()
                      .HasForeignKey(e => e.BlockedId)
                      // Aynı tabloya ikinci cascade yolu; SQL Server izin vermez (bkz. Follow).
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Report>(entity =>
            {
                entity.Property(e => e.ReporterId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.TargetUserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.ResolvedById).HasMaxLength(450);
                entity.Property(e => e.Note).HasMaxLength(1000);
                entity.Property(e => e.ResolutionNote).HasMaxLength(1000);

                // Moderasyon kuyruğu açık bildirimleri tarihe göre okur
                // (imleç (CreatedAt, Id) çiftinden ilerliyor).
                entity.HasIndex(e => new { e.Status, e.CreatedAt, e.Id });
                // "Aynı içerik için başka bildirim var mı" ve mükerrer kontrolü.
                entity.HasIndex(e => new { e.TargetType, e.TargetId });
                entity.HasIndex(e => new { e.TargetUserId, e.Status });

                entity.HasOne(e => e.Reporter)
                      .WithMany()
                      .HasForeignKey(e => e.ReporterId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Aşağıdaki iki ilişki de AppUser cascade'i ile çoklu yol oluşturur.
                entity.HasOne(e => e.TargetUser)
                      .WithMany()
                      .HasForeignKey(e => e.TargetUserId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.ResolvedBy)
                      .WithMany()
                      .HasForeignKey(e => e.ResolvedById)
                      .OnDelete(DeleteBehavior.NoAction);

                // TargetId polimorfik olduğu için FK yok; içerik silinince bildirim
                // kayıtta kalır — moderasyon geçmişi içerikle birlikte silinmemeli.
            });

            modelBuilder.Entity<DailyTrafficStat>(entity =>
            {
                // Gün başına tek satır; yazıcı önce bu indeksten satırı bulup
                // üzerine ekliyor (bkz. MetricsFlushService).
                entity.HasIndex(e => e.Day).IsUnique();
            });

            modelBuilder.Entity<DailyActiveUser>(entity =>
            {
                // Gün + kullanıcı tekil: aynı kişi gün içinde kaç istek atarsa
                // atsın tek satır. Panel "bugün kaç kişi" sorusunu bu tabloyu
                // sayarak cevaplıyor, DISTINCT taramasına gerek kalmıyor.
                entity.HasIndex(e => new { e.Day, e.UserId }).IsUnique();

                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
