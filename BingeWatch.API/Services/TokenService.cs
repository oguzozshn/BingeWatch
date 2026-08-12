using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BingeWatch.API.Models;
using Microsoft.IdentityModel.Tokens;

namespace BingeWatch.API.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreateToken(AppUser user, IEnumerable<string> roles)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new("display_name", user.DisplayName),

                // Token'ın tek iptal kolu. Şifre değişince Identity bu damgayı
                // yeniliyor ve eski damgayı taşıyan token'lar reddediliyor —
                // aksi halde token süresi (7 gün) dolana kadar geçerli kalırdı.
                new(TokenStampValidator.ClaimType, user.SecurityStamp ?? string.Empty)
            };

            // [Authorize(Roles = ...)] varsayılan olarak ClaimTypes.Role'e bakar.
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
