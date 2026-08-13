using System.Security.Claims;
using System.Text;
using BingeWatch.API.Configurations;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    // Kayıt ve giriş parola denemesine açık; ikisi de IP başına dar kotada.
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IPasswordResetNotifier _notifier;
        private readonly ITokenStampValidator _stampValidator;
        private readonly ILogger<AuthController> _logger;

        public AuthController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager,
            ITokenService tokenService, IPasswordResetNotifier notifier,
            ITokenStampValidator stampValidator, ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _notifier = notifier;
            _stampValidator = stampValidator;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Username, email and password are required" });

            var user = new AppUser
            {
                UserName = request.Username,
                Email = request.Email,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username : request.DisplayName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

            // Yeni kullanıcının hiç rolü yok; yine de tek bir yerden üretelim.
            return Ok(await BuildAuthResponseAsync(user));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByNameAsync(request.UsernameOrEmail)
                       ?? await _userManager.FindByEmailAsync(request.UsernameOrEmail);

            if (user == null)
                return Unauthorized(new { message = "Invalid credentials" });

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
                return Unauthorized(new { message = "Invalid credentials" });

            return Ok(await BuildAuthResponseAsync(user));
        }

        /// <summary>
        /// Sıfırlama bağlantısı ister.
        /// </summary>
        /// <remarks>
        /// E-posta kayıtlı olsun ya da olmasın <b>her zaman 200</b> döner.
        /// "Böyle bir kullanıcı yok" demek, kimin üye olduğunu tek tek sorarak
        /// öğrenmeye izin verirdi (hesap sayımı). Aynı sebeple yanıt gövdesi de
        /// sabit.
        /// </remarks>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!_notifier.IsEnabled)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "Şifre sıfırlama şu an kullanılamıyor." });
            }

            var user = string.IsNullOrWhiteSpace(request.Email)
                ? null
                : await _userManager.FindByEmailAsync(request.Email);

            if (user?.Email != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                // Token base64 değil ham metin ve '+' gibi karakterler taşıyor;
                // sorgu dizesinde kodlanmadan gönderilirse bozuluyor.
                var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

                var resetUrl = QueryHelpers.AddQueryString(
                    request.ResetUrlBase,
                    new Dictionary<string, string?>
                    {
                        ["email"] = user.Email,
                        ["token"] = encoded
                    });

                try
                {
                    await _notifier.SendAsync(user.Email, resetUrl);
                }
                catch (Exception ex)
                {
                    // Gönderim hatası yanıta yansıtılmamalı. Kayıtlı olmayan
                    // adres için gönderim hiç denenmiyor, yani hata 500'e
                    // dönseydi "500 = hesap var, 200 = yok" gibi bir sızıntı
                    // olurdu — tam da kaçınmaya çalıştığımız hesap sayımı.
                    _logger.LogError(ex, "Sifirlama e-postasi gonderilemedi.");
                }
            }

            return Ok(new { message = "Kayıtlı bir hesap varsa sıfırlama bağlantısı gönderildi." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email)
                || string.IsNullOrWhiteSpace(request.Token)
                || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Eksik bilgi." });
            }

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                // Burada da hesabın varlığı sızmamalı: geçersiz token ile aynı yanıt.
                return BadRequest(new { message = "Bağlantı geçersiz ya da süresi dolmuş." });
            }

            string token;
            try
            {
                token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
            }
            catch (FormatException)
            {
                // Elle kurcalanmış bağlantı; çözülemeyen token da geçersiz token.
                return BadRequest(new { message = "Bağlantı geçersiz ya da süresi dolmuş." });
            }

            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
            if (!result.Succeeded)
            {
                // Parola kuralı hataları kullanıcıya lazım; token hatası değil.
                var passwordErrors = result.Errors
                    .Where(e => !e.Code.Contains("Token", StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.Description)
                    .ToList();

                return BadRequest(new
                {
                    message = passwordErrors.Count > 0
                        ? string.Join("; ", passwordErrors)
                        : "Bağlantı geçersiz ya da süresi dolmuş."
                });
            }

            // Sıfırlama sonrası kilit kalkmalı; aksi halde doğru parolayla bile
            // giremiyor ve neden olduğunu anlamıyor.
            await _userManager.ResetAccessFailedCountAsync(user);

            // Sıfırlama da damgayı yeniliyor: "şifremi unuttum" akışının ardından
            // eski oturumların ayakta kalması, akışın amacına ters.
            _stampValidator.Invalidate(user.Id);

            return Ok(new { message = "Parolan güncellendi." });
        }

        /// <summary>
        /// Giriş yapmış kullanıcının şifresini değiştirir ve <b>diğer oturumları
        /// düşürür.</b>
        /// </summary>
        /// <remarks>
        /// Identity şifre değişince <c>SecurityStamp</c>'i yeniliyor; damga
        /// token'da claim olarak durduğu için eski damgalı token'lar bir sonraki
        /// istekte 401 alıyor. Kullanıcının kendi oturumu düşmesin diye yanıt
        /// taze bir token taşıyor — Web bunu cookie'ye yazıp devam ediyor.
        ///
        /// Sınıf seviyesindeki dar kota (IP başına 10/5dk) burada da geçerli:
        /// mevcut şifre soruluyor, yani bu uç da bir parola deneme yüzeyi.
        /// </remarks>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword)
                || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Mevcut ve yeni şifre gerekli." });
            }

            var user = await _userManager.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (user == null)
                return Unauthorized();

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                // "Mevcut şifre yanlış" ile "yeni şifre kurala uymuyor" ayrı
                // sorunlar; ikisini tek mesaja katlamak kullanıcıyı hangisini
                // düzelteceğini bilmeden bırakırdı.
                if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
                    return BadRequest(new { message = "Mevcut şifren doğru değil." });

                return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });
            }

            // Damga veritabanında değişti; önbellekteki eski değer düşürülmezse
            // iptal bu süreçte önbellek ömrü kadar gecikirdi.
            _stampValidator.Invalidate(user.Id);

            return Ok(await BuildAuthResponseAsync(user));
        }

        /// <summary>
        /// Roller token'a claim olarak giriyor; Web tarafı bu token'ı cookie'de
        /// taşıdığı için moderasyon menüsünü ayrıca sormadan gösterebiliyor.
        /// </summary>
        private async Task<AuthResponse> BuildAuthResponseAsync(AppUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            return new AuthResponse
            {
                Token = _tokenService.CreateToken(user, roles),
                UserId = user.Id,
                Username = user.UserName!,
                DisplayName = user.DisplayName,
                Roles = roles.ToList()
            };
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null)
                return NotFound();

            return Ok(new UserProfileDto
            {
                Username = user.UserName!,
                DisplayName = user.DisplayName,
                Bio = user.Bio,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt
            });
        }
    }
}
