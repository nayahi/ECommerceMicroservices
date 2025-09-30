namespace ECommerce.Common.Configuration
{
    public class RabbitMQSettings
    {
        public string Host { get; set; } = "localhost";
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";
        public int Port { get; set; } = 5672;

        public string GetConnectionString()
        {
            return $"amqp://{Username}:{Password}@{Host}:{Port}{VirtualHost}";
        }
    }

    public class ServiceUrls
    {
        public string CatalogService { get; set; } = "http://localhost:5001";
        public string UserService { get; set; } = "http://localhost:5002";
        public string OrderService { get; set; } = "http://localhost:5003";
        public string PaymentService { get; set; } = "http://localhost:5004";
        public string InventoryService { get; set; } = "http://localhost:5005";
        public string NotificationService { get; set; } = "http://localhost:5006";
    }
}