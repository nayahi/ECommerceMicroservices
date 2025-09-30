using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using BC = BCrypt.Net.BCrypt;

namespace UserService.Services
{
    public class UserECService : IUserService
    {
        private readonly UserDbContext _context;
        private readonly ILogger<UserECService> _logger;
        private readonly string[] _euDomains = { ".es", ".fr", ".de", ".it", ".nl", ".be", ".pt", ".gr", ".pl", ".eu" };

        public UserECService(UserDbContext context, ILogger<UserECService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<UserDto> RegisterAsync(RegisterDto dto)
        {
            // Verificar si el email ya existe
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                throw new InvalidOperationException("Email ya fue registrado");
            }

            var user = new User
            {
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                PasswordHash = BC.HashPassword(dto.Password),
                Role = "Customer"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuario registrado con ID {UserId}", user.Id);

            return MapToDto(user);
        }

        public async Task<UserDto?> ValidateCredentialsAsync(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

            if (user == null || !BC.Verify(password, user.PasswordHash))
            {
                return null;
            }

            user.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapToDto(user);
        }

        public async Task<UserDto?> GetUserByIdAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            return user == null ? null : MapToDto(user);
        }

        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            return await _context.Users
                .Where(u => u.IsActive)
                .Select(u => MapToDto(u))
                .ToListAsync();
        }

        public async Task<bool> ValidateEmailGDPRAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Validar si el email es de un dominio europeo
            return _euDomains.Any(domain => user.Email.EndsWith(domain, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<decimal?> GetUserDiscountAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return null;

            // Premium users get 15% discount
            return user.Role == "Premium" ? 0.15m : 0m;
        }

        private static UserDto MapToDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };
        }
    }
}
