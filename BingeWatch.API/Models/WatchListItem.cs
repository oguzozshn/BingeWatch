using System.ComponentModel.DataAnnotations;

namespace BingeWatch.API.Models
{
    public class WatchListItem
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int SeriesId { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string SeriesName { get; set; } = string.Empty;
        
        [MaxLength(2000)]
        public string Overview { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string PosterPath { get; set; } = string.Empty;
        
        public DateTime? FirstAirDate { get; set; }
        
        public DateTime AddedDate { get; set; } = DateTime.UtcNow;

        public bool IsInWatchList { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty; // Basit kullanıcı kimliği için
    }
} 