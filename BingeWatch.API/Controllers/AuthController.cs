using System.Security.Claims;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
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

            var token = _tokenService.CreateToken(user);
            return Ok(new AuthResponse
            {
                Token = token,
                UserId = user.Id,
                Username = user.UserName!,
                DisplayName = user.DisplayName
            });
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

            var token = _tokenService.CreateToken(user);
            return Ok(new AuthResponse
            {
                Token = token,
                UserId = user.Id,
                Username = user.UserName!,
                DisplayName = user.DisplayName
            });
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
