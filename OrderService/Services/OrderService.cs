using ECommerce.Common.DTOs;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Models;

namespace OrderService.Services
{
    public class OrderECService : IOrderService
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<OrderECService> _logger;

        public OrderECService(OrderDbContext context, ILogger<OrderECService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PagedResponse<OrderDto>> GetOrdersAsync(int pageNumber, int pageSize)
        {
            var totalItems = await _context.Orders.CountAsync();

            var orders = await _context.Orders
                .Include(o => o.Items)
                .OrderByDescending(o => o.OrderDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(o => MapToDto(o))
                .ToListAsync();

            return new PagedResponse<OrderDto>
            {
                Items = orders,
                TotalItems = totalItems,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<OrderDto?> GetOrderByIdAsync(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            return order == null ? null : MapToDto(order);
        }

        public async Task<List<OrderDto>> GetOrdersByUserAsync(int userId)
        {
            return await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => MapToDto(o))
                .ToListAsync();
        }

        public async Task<OrderDto> CreateOrderAsync(Order order)
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Pedido creado con ID {OrderId}", order.Id);

            return MapToDto(order);
        }

        public async Task<OrderDto?> UpdateOrderStatusAsync(int id, string status)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return null;

            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Pedido {OrderId} actualizado a {Status}", id, status);

            return MapToDto(order);
        }

        public async Task<OrderDto?> CancelOrderAsync(int id)
        {
            return await UpdateOrderStatusAsync(id, "Cancelled");
        }

        private static OrderDto MapToDto(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                UserId = order.UserId,
                OrderDate = order.OrderDate,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                ShippingAddress = order.ShippingAddress,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Subtotal = i.Subtotal
                }).ToList()
            };
        }
    }
}
