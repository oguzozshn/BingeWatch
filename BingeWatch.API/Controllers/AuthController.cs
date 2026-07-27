using System.Security.Claims;
using BingeWatch.API.Configurations;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

        public AuthController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ITokenService tokenService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
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
