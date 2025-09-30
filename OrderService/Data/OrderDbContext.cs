using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data
{
    //Add-Migration InitialCreate -Project OrderService -StartupProject OrderService
    //Update - Database -Project OrderService -StartupProject OrderService
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
        {
        }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ShippingAddress).IsRequired().HasMaxLength(500);
                entity.HasMany(e => e.Items).WithOne(e => e.Order).HasForeignKey(e => e.OrderId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Status);
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            });
        }
    }
}
