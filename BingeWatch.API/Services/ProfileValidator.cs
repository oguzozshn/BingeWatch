using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// Profil düzenleme kuralları. Controller'ın içinde durduğu sürece test
    /// edilemiyordu; saf bir fonksiyon olarak dışarı alındı.
    /// </summary>
    public static class ProfileValidator
    {
        public const int MaxDisplayNameLength = 50;
        public const int MaxBioLength = 300;

        /// <summary>
        /// Girdiyi kırpar ve doğrular. Hata varsa <paramref name="error"/> dolu
        /// döner; yoksa <paramref name="clean"/> kaydedilmeye hazırdır.
        /// </summary>
        public static bool TryNormalize(UpdateProfileRequest request, out UpdateProfileRequest clean, out string? error)
        {
            clean = new UpdateProfileRequest { IsPrivate = request.IsPrivate };
            error = null;

            var displayName = request.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = "Görünen ad boş olamaz.";
                return false;
            }

            if (displayName.Length > MaxDisplayNameLength)
            {
                error = $"Görünen ad en fazla {MaxDisplayNameLength} karakter olabilir.";
                return false;
            }

            var bio = request.Bio?.Trim();
            if (bio?.Length > MaxBioLength)
            {
                error = $"Hakkında en fazla {MaxBioLength} karakter olabilir.";
                return false;
            }

            var avatarUrl = request.AvatarUrl?.Trim();
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                if (!Uri.TryCreate(avatarUrl, UriKind.Absolute, out var parsed))
                {
                    error = "Avatar adresi geçerli bir URL olmalı.";
                    return false;
                }

                // javascript: ve data: adresleri <img src> üzerinden saldırı
                // yüzeyi açar; şema beyaz listeyle sınırlı.
                if (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)
                {
                    error = "Avatar adresi http ya da https olmalı.";
                    return false;
                }
            }

            clean.DisplayName = displayName;
            // Boş metin ile null aynı şey: "girilmemiş".
            clean.Bio = string.IsNullOrWhiteSpace(bio) ? null : bio;
            clean.AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl;
            return true;
        }
    }
}
