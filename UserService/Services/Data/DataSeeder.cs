using UserService.Data;
using UserService.Models;
using BC = BCrypt.Net.BCrypt;

namespace UserService.Services.Data
{
    public static class DataSeeder
    {
        public static void SeedUsers(UserDbContext context)
        {
            var users = new List<User>
        {
            new User
            {
                Email = "juan.perez@empresa.es",
                FirstName = "Juan",
                LastName = "Pérez",
                PasswordHash = BC.HashPassword("Password123!"),
                Role = "Premium"
            },
            new User
            {
                Email = "marie.dubois@societe.fr",
                FirstName = "Marie",
                LastName = "Dubois",
                PasswordHash = BC.HashPassword("Password123!"),
                Role = "Customer"
            },
            new User
            {
                Email = "john.doe@company.com",
                FirstName = "John",
                LastName = "Doe",
                PasswordHash = BC.HashPassword("Password123!"),
                Role = "Premium"
            },
            new User
            {
                Email = "admin@tienda.mx",
                FirstName = "Admin",
                LastName = "System",
                PasswordHash = BC.HashPassword("Admin123!"),
                Role = "Admin"
            }
        };

            context.Users.AddRange(users);
            context.SaveChanges();
        }
    }
}
