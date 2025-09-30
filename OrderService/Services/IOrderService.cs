using ECommerce.Common.DTOs;
using OrderService.DTOs;
using OrderService.Models;

namespace OrderService.Services
{
    public interface IOrderService
    {
        Task<PagedResponse<OrderDto>> GetOrdersAsync(int pageNumber, int pageSize);
        Task<OrderDto?> GetOrderByIdAsync(int id);
        Task<List<OrderDto>> GetOrdersByUserAsync(int userId);
        Task<OrderDto> CreateOrderAsync(Order order);
        Task<OrderDto?> UpdateOrderStatusAsync(int id, string status);
        Task<OrderDto?> CancelOrderAsync(int id);
    }
}
