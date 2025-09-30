using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using UserService.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

//Add-Migration InitialCreate -Project UserService -StartupProject UserService
//Update - Database - Project UserService - StartupProject UserService

//"email": "jzuniga@jairozuniga.com",
//  "password": "HolaEsteUnPass!complicado1",
//  "firstName": "Jairo",
//  "lastName": "Zuniga"
namespace UserService.Data
{
    public class UserDbContext : DbContext
    {
        public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            });
        }
    }
}
