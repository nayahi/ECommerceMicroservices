using OrderService.DTOs;

namespace OrderService.Services
{
    public interface IOrderSagaOrchestrator
    {
        Task<OrderDto> CreateOrderAsync(CreateOrderDto dto);
    }
}
