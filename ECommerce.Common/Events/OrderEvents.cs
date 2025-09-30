using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Common.Events
{
    // Evento base con información común
    public abstract class BaseEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    }

    // Eventos del dominio de pedidos
    public class OrderCreatedEvent : BaseEvent
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class OrderItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class StockReservedEvent : BaseEvent
    {
        public int OrderId { get; set; }
        public Dictionary<int, int> ReservedItems { get; set; } = new();
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class PaymentProcessedEvent : BaseEvent
    {
        public int OrderId { get; set; }
        public bool Success { get; set; }
        public string TransactionId { get; set; }
        public string Message { get; set; }
    }

    public class OrderConfirmedEvent : BaseEvent
    {
        public int OrderId { get; set; }
        public string UserEmail { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class OrderCancelledEvent : BaseEvent
    {
        public int OrderId { get; set; }
        public string Reason { get; set; }
    }
}
