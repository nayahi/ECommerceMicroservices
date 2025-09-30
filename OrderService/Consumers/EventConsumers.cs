using ECommerce.Common.Events;
using MassTransit;
using OrderService.Services;

namespace OrderService.Consumers
{
    public class StockReservedConsumer : IConsumer<StockReservedEvent>
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<StockReservedConsumer> _logger;

        public StockReservedConsumer(IOrderService orderService, ILogger<StockReservedConsumer> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<StockReservedEvent> context)
        {
            var message = context.Message;

            _logger.LogInformation("Received StockReservedEvent for Order {OrderId}: Success={Success}",
                message.OrderId, message.Success);

            if (!message.Success)
            {
                // Si no se pudo reservar el stock, cancelar el pedido
                await _orderService.UpdateOrderStatusAsync(message.OrderId, "Cancelled");
                _logger.LogWarning("Order {OrderId} cancelled due to insufficient stock", message.OrderId);
            }
        }
    }

    public class PaymentProcessedConsumer : IConsumer<PaymentProcessedEvent>
    {
        private readonly IOrderService _orderService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<PaymentProcessedConsumer> _logger;

        public PaymentProcessedConsumer(
            IOrderService orderService,
            IPublishEndpoint publishEndpoint,
            ILogger<PaymentProcessedConsumer> logger)
        {
            _orderService = orderService;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
        {
            var message = context.Message;

            _logger.LogInformation("Evento de procesamiento de pago recibido para el pedido {OrderId}: Exito={Success}",
                message.OrderId, message.Success);

            if (message.Success)
            {
                // Pago exitoso, confirmar el pedido
                var order = await _orderService.UpdateOrderStatusAsync(message.OrderId, "Confirmed");

                if (order != null)
                {
                    // Publicar evento de confirmación
                    await _publishEndpoint.Publish(new OrderConfirmedEvent
                    {
                        OrderId = order.Id,
                        UserEmail = "", // Aquí deberías obtener el email del usuario
                        TotalAmount = order.TotalAmount
                    });

                    _logger.LogInformation("Pedido {OrderId} confirmado", message.OrderId);
                }
            }
            else
            {
                // Pago fallido, cancelar el pedido
                await _orderService.UpdateOrderStatusAsync(message.OrderId, "PaymentFailed");
                _logger.LogWarning("Pago del pedido {OrderId} fallido: {Message}",
                    message.OrderId, message.Message);
            }
        }
    }
}
