namespace BingeWatch.API.Services
{
    /// <summary>
    /// JWT'nin taşıdığı güvenlik damgasının hâlâ geçerli olup olmadığını söyler.
    /// Token stateless olduğu için tek iptal yolu bu: şifre değişince Identity
    /// kullanıcının <c>SecurityStamp</c>'ini yeniliyor, eski damgayı taşıyan
    /// token'lar da böylece geçersizleşiyor.
    /// </summary>
    public interface ITokenStampValidator
    {
        /// <summary>
        /// Token'daki damga kullanıcının güncel damgasıyla eşleşiyor mu?
        /// Damgasız token (bu özellik öncesinde üretilmiş) <c>false</c> döner —
        /// iptal edilemeyen bir token'a güvenmek özelliği anlamsız kılardı.
        /// </summary>
        Task<bool> IsCurrentAsync(string userId, string? stamp);

        /// <summary>
        /// Damga değiştiği anda önbelleği düşürür; aynı süreçteki eski token'lar
        /// önbellek süresini beklemeden geçersiz olsun diye.
        /// </summary>
        void Invalidate(string userId);
    }
}
