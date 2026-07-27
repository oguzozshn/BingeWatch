using BingeWatch.API.Models;

namespace BingeWatch.API.Dtos
{
    /// <summary>Akıştaki tek bir olay — kart çizmek için gereken her şeyi taşır, ek istek gerekmez.</summary>
    public class ActivityDto
    {
        public int Id { get; set; }
        public ActivityType Type { get; set; }
        public DateTime CreatedAt { get; set; }

        // Olayı üreten
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        // Dizi hedefli olaylar
        public int? TmdbShowId { get; set; }
        public string? ShowName { get; set; }
        public string? ShowPosterPath { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeNumber { get; set; }
        public string? EpisodeName { get; set; }

        /// <summary>Toplu izlemede işaretlenen bölüm sayısı.</summary>
        public int? EpisodeCount { get; set; }

        public decimal? RatingValue { get; set; }

        public int? ReviewId { get; set; }
        public string? ReviewExcerpt { get; set; }
        public bool ReviewHasSpoilers { get; set; }

        // Takip olayı
        public string? TargetUsername { get; set; }
        public string? TargetDisplayName { get; set; }
    }
}
