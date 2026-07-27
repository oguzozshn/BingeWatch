using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    public interface ITokenService
    {
        /// <summary>Roller token'a claim olarak yazılır; API tarafı yetkilendirmeyi buradan okur.</summary>
        string CreateToken(AppUser user, IEnumerable<string> roles);
    }
}
