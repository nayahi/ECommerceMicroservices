using ECommerce.Common.Events;
using MassTransit;
using OrderService.DTOs;
using OrderService.Models;

namespace OrderService.Services
{
    public class OrderSagaOrchestrator : IOrderSagaOrchestrator
    {
        private readonly IOrderService _orderService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<OrderSagaOrchestrator> _logger;

        public OrderSagaOrchestrator(
            IOrderService orderService,
            IHttpClientFactory httpClientFactory,
            IPublishEndpoint publishEndpoint,
            ILogger<OrderSagaOrchestrator> logger)
        {
            _orderService = orderService;
            _httpClientFactory = httpClientFactory;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto)
        {
            try
            {
                // 1. Validar usuario
                var userClient = _httpClientFactory.CreateClient("UserService");
                var userResponse = await userClient.GetAsync($"/api/users/{dto.UserId}");

                if (!userResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Usuario {dto.UserId} no se encontro");
                }

                // 2. Validar productos y obtener información
                var catalogClient = _httpClientFactory.CreateClient("CatalogService");
                var orderItems = new List<OrderItem>();
                decimal totalAmount = 0;

                foreach (var item in dto.Items)
                {
                    var productResponse = await catalogClient.GetAsync($"/api/products/{item.ProductId}");

                    if (!productResponse.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException($"Producto {item.ProductId} no encontrado");
                    }

                    var productJson = await productResponse.Content.ReadAsStringAsync();
                    var product = System.Text.Json.JsonSerializer.Deserialize<ProductInfo>(productJson);

                    if (product?.Data != null)
                    {
                        orderItems.Add(new OrderItem
                        {
                            ProductId = item.ProductId,
                            ProductName = product.Data.Name,
                            Quantity = item.Quantity,
                            UnitPrice = product.Data.Price
                        });

                        totalAmount += product.Data.Price * item.Quantity;
                    }
                }

                // 3. Crear el pedido localmente (estado: Pending)
                var order = new Order
                {
                    UserId = dto.UserId,
                    ShippingAddress = dto.ShippingAddress,
                    Items = orderItems,
                    TotalAmount = totalAmount,
                    Status = "Pending"
                };

                var createdOrder = await _orderService.CreateOrderAsync(order);

                // 4. Publicar evento OrderCreated para iniciar la saga
                await _publishEndpoint.Publish(new OrderCreatedEvent
                {
                    OrderId = createdOrder.Id,
                    UserId = dto.UserId,
                    TotalAmount = totalAmount,
                    Items = createdOrder.Items.Select(i => new ECommerce.Common.Events.OrderItemDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        Price = i.UnitPrice
                    }).ToList()
                });

                _logger.LogInformation("Pedido saga para el pedido {OrderId}", createdOrder.Id);

                return createdOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error la orquestacion Saga");
                throw;
            }
        }

        // Clase auxiliar para deserializar respuesta de producto
        private class ProductInfo
        {
            public ProductData? Data { get; set; }
        }

        private class ProductData
        {
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
        }
    }
}
