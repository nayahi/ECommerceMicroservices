using UserService.DTOs;

namespace UserService.Services
{
    public interface ITokenService
    {
        string GenerateToken(UserDto user);
    }
}
