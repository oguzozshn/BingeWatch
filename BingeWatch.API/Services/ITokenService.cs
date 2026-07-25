using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    public interface ITokenService
    {
        string CreateToken(AppUser user);
    }
}
