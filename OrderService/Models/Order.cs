namespace OrderService.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending"; // Pending, Processing, Confirmed, Shipped, Delivered, Cancelled
        public decimal TotalAmount { get; set; }
        public string ShippingAddress { get; set; } = string.Empty;
        public List<OrderItem> Items { get; set; } = new();
        public string? PaymentTransactionId { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal => Quantity * UnitPrice;
    }
}
