using Microsoft.EntityFrameworkCore;
using ECommerce.Common.Extensions;
using ECommerce.Common.DTOs;
using ECommerce.Common.Events;
using ECommerce.Common.Configuration;
using MassTransit;
using Serilog;
using FluentValidation;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Services;
using OrderService.Consumers;
using MassTransit;
using ECommerce.Common.Events;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
builder.ConfigureSerilog("OrderECService");

// Agregar servicios
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Order Service API", Version = "v1" });
});

// Configurar Entity Framework sin docker
//builder.Services.AddDbContext<OrderDbContext>(options =>
//    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<OrderDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

// Configurar HttpClients para comunicación con otros servicios
var serviceUrls = builder.Configuration.GetSection("ServiceUrls").Get<ServiceUrls>() ?? new ServiceUrls();
builder.Services.AddSingleton(serviceUrls);

builder.Services.AddResilientHttpClient("CatalogService", serviceUrls.CatalogService);
builder.Services.AddResilientHttpClient("UserService", serviceUrls.UserService);


builder.Services.AddMassTransit(x =>
{
    // Configurar consumidores aquí
    x.AddConsumer<StockReservedConsumer>();
    x.AddConsumer<PaymentProcessedConsumer>();
    x.AddConsumer<UserCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        // Leer configuración de RabbitMQ
        var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "admin";
        var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "admin123";

        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ReceiveEndpoint("order-service-queue", e =>
        {
            e.ConfigureConsumer<UserCreatedConsumer>(context);
        });

        //cfg.ConfigureEndpoints(context);
    });
});

// Configurar MassTransit con RabbitMQ sin docker
//builder.Services.AddMassTransit(x =>
//{
//    x.AddConsumer<StockReservedConsumer>();
//    x.AddConsumer<PaymentProcessedConsumer>();

//    x.UsingRabbitMq((context, cfg) =>
//    {
//        var rabbitSettings = builder.Configuration.GetSection("RabbitMQ").Get<RabbitMQSettings>() ?? new RabbitMQSettings();

//        cfg.Host(rabbitSettings.Host, rabbitSettings.VirtualHost, h =>
//        {
//            h.Username(rabbitSettings.Username);
//            h.Password(rabbitSettings.Password);
//        });

//        cfg.ConfigureEndpoints(context);
//    });
//});

// Registrar servicios
builder.Services.AddScoped<IOrderService, OrderECService>();
builder.Services.AddScoped<IOrderSagaOrchestrator, OrderSagaOrchestrator>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Health checks
builder.Services.AddStandardHealthChecks(builder.Configuration.GetConnectionString("DefaultConnection"));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configurar HttpClient para comunicación síncrona
builder.Services.AddHttpClient("UserService", client =>
{
    client.BaseAddress = new Uri("http://localhost:5002/"); // Puerto de UserService
client.Timeout = TimeSpan.FromSeconds(30);
});


// Configurar RabbitMQ
//builder.Services.AddMassTransit(x =>
//{
//    x.AddConsumer<UserCreatedConsumer>();

//    x.UsingRabbitMq((context, cfg) =>
//    {
//        cfg.Host("localhost", "/", h =>
//        {
//            h.Username("admin");
//            h.Password("admin123");
//        });

//        cfg.ReceiveEndpoint("order-service-queue", e =>
//        {
//            e.ConfigureConsumer<UserCreatedConsumer>(context);
//        });
//    });
//});

var app = builder.Build();

// Middleware pipeline
app.UseCorrelationId();
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Aplicar migraciones automáticamente
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    dbContext.Database.Migrate();
}

// ============================================
// ENDPOINTS
// ============================================

// POST /api/orders - Crear nuevo pedido
app.MapPost("/api/orders", async (
    CreateOrderDto dto,
    IOrderSagaOrchestrator orchestrator,
    IValidator<CreateOrderDto> validator) =>
{
    var validationResult = await validator.ValidateAsync(dto);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(ApiResponse<OrderDto>.Fail(
            "Validacion fallida",
            validationResult.Errors.Select(e => e.ErrorMessage).ToList()));
    }

    try
    {
        var order = await orchestrator.CreateOrderAsync(dto);
        return Results.Created($"/api/orders/{order.Id}",
            ApiResponse<OrderDto>.Ok(order, "Pedido creado ok"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error al crear el pedido");
        return Results.Problem("Un error occurrio mientras se creaba el pedido");
    }
})
.WithName("CreateOrder")
.Produces<ApiResponse<OrderDto>>(201)
.Produces(400)
.WithOpenApi();

// GET /api/orders - Obtener todos los pedidos
app.MapGet("/api/orders", async (
    IOrderService orderService,
    int pageNumber = 1,
    int pageSize = 10) =>
{
    try
    {
        var orders = await orderService.GetOrdersAsync(pageNumber, pageSize);
        return Results.Ok(ApiResponse<PagedResponse<OrderDto>>.Ok(orders));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error al obtener pedidos");
        return Results.Problem("Un error occurrio mientras se obtenian los pedidos");
    }
})
.WithName("GetOrders")
.Produces<ApiResponse<PagedResponse<OrderDto>>>(200)
.WithOpenApi();

// GET /api/orders/{id} - Obtener pedido por ID
app.MapGet("/api/orders/{id:int}", async (int id, IOrderService orderService) =>
{
    try
    {
        var order = await orderService.GetOrderByIdAsync(id);
        if (order == null)
            return Results.NotFound(ApiResponse<OrderDto>.Fail($"Pedido con ID {id} no se encontro"));

        return Results.Ok(ApiResponse<OrderDto>.Ok(order));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error al obtener el pedido {OrderId}", id);
        return Results.Problem($"Un error occurrio mientras se obtenia el pedido {id}");
    }
})
.WithName("GetOrderById")
.Produces<ApiResponse<OrderDto>>(200)
.Produces(404)
.WithOpenApi();

// GET /api/orders/user/{userId} - Obtener pedidos de un usuario
app.MapGet("/api/orders/user/{userId:int}", async (
    int userId,
    IOrderService orderService) =>
{
    try
    {
        var orders = await orderService.GetOrdersByUserAsync(userId);
        return Results.Ok(ApiResponse<List<OrderDto>>.Ok(orders));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error al obtener los pedidos del usuario {UserId}", userId);
        return Results.Problem($"Un error occurrio mientras se obtenian los pedidos del usuario {userId}");
    }
})
.WithName("GetOrdersByUser")
.Produces<ApiResponse<List<OrderDto>>>(200)
.WithOpenApi();

// PUT /api/orders/{id}/status - Actualizar estado del pedido
app.MapPut("/api/orders/{id:int}/status", async (
    int id,
    UpdateOrderStatusDto dto,
    IOrderService orderService) =>
{
    try
    {
        var order = await orderService.UpdateOrderStatusAsync(id, dto.Status);
        if (order == null)
            return Results.NotFound(ApiResponse<OrderDto>.Fail($"Pedido con ID {id} no se encontro"));

        return Results.Ok(ApiResponse<OrderDto>.Ok(order, "Estado del Pedido actualizado ok"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error al actualizar el estado del pedido {OrderId}", id);
        return Results.Problem($"Un error occurrio mientras se actualizaba el pedido {id}");
    }
})
.WithName("UpdateOrderStatus")
.Produces<ApiResponse<OrderDto>>(200)
.Produces(404)
.WithOpenApi();

// POST /api/orders/{id}/cancel - Cancelar pedido
app.MapPost("/api/orders/{id:int}/cancel", async (
    int id,
    IOrderService orderService,
    IPublishEndpoint publishEndpoint) =>
{
    try
    {
        var order = await orderService.CancelOrderAsync(id);
        if (order == null)
            return Results.NotFound(ApiResponse<OrderDto>.Fail($"Pedico con ID {id} no se encontro"));

        // Publicar evento de cancelación
        await publishEndpoint.Publish(new OrderCancelledEvent
        {
            OrderId = id,
            Reason = "Cancelled by user"
        });

        return Results.Ok(ApiResponse<OrderDto>.Ok(order, "Order cancelado ok"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error al cancelar el pedido {OrderId}", id);
        return Results.Problem($"Un error occurrio mientras se cancelaba el pedido {id}");
    }
})
.WithName("CancelOrder")
.Produces<ApiResponse<OrderDto>>(200)
.Produces(404)
.WithOpenApi();

///////////////////////////
///
// ENDPOINT: Crear orden simple (HTTP + Evento)
app.MapPost("/api/orders/simple", async Task<IResult> (
    SimpleCreateOrderDto dto,
    IHttpClientFactory httpClientFactory,
    IPublishEndpoint publishEndpoint) =>
{
    try
    {
        Log.Information("📦 Creando orden para usuario {UserId}", dto.UserId);

        // PASO 1: Validar usuario via HTTP
        var httpClient = httpClientFactory.CreateClient("UserService");

        try
        {
            var response = await httpClient.GetAsync($"api/users/{dto.UserId}");

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("❌ Usuario {UserId} no encontrado", dto.UserId);
                return Results.BadRequest(new
                {
                    success = false,
                    message = $"Usuario {dto.UserId} no existe"
                });
            }

            var userJson = await response.Content.ReadAsStringAsync();
            Log.Information("✅ Usuario validado via HTTP");
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "⚠️ UserService no disponible");

            // OPCIÓN 1: Usar StatusCode genérico para 503
            return Results.Problem(
                detail: "UserService no disponible temporalmente",
                statusCode: 503,
                title: "Service Unavailable"
            );

            // OPCIÓN 2: Usar Json con StatusCode específico
            // return Results.Json(
            //     new { success = false, message = "UserService no disponible temporalmente" },
            //     statusCode: 503
            // );
        }

        // PASO 2: Crear la orden
        var orderId = Random.Shared.Next(1000, 9999);
        var order = new
        {
            Id = orderId,
            UserId = dto.UserId,
            TotalAmount = dto.TotalAmount,
            Status = "Created",
            CreatedAt = DateTime.UtcNow
        };

        // PASO 3: Publicar evento
        var orderEvent = new SimpleOrderCreatedEvent
        {
            OrderId = orderId,
            UserId = dto.UserId,
            TotalAmount = dto.TotalAmount
        };

        await publishEndpoint.Publish(orderEvent);
        Log.Information("📤 Evento SimpleOrderCreated publicado");

        return Results.Created($"/api/orders/{orderId}", new
        {
            success = true,
            message = "Orden creada exitosamente",
            data = order,
            eventPublished = true
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error creando orden");
        return Results.Problem("Error al crear la orden");
    }
})
.WithName("CreateSimpleOrder")
.WithOpenApi()
.Produces<object>(201)
.Produces<object>(400)
.ProducesProblem(503)
.ProducesProblem(500);


// ENDPOINT: Probar comunicación HTTP
app.MapGet("/api/test/check-user/{userId}", async (
    int userId,
    IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("UserService");

    try
    {
        var response = await httpClient.GetAsync($"api/users/{userId}");

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return Results.Ok(new
            {
                success = true,
                message = "Usuario encontrado via HTTP",
                statusCode = (int)response.StatusCode,
                data = JsonSerializer.Deserialize<object>(content)
            });
        }

        return Results.NotFound(new
        {
            success = false,
            message = "Usuario no encontrado",
            statusCode = (int)response.StatusCode
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error comunicándose con UserService: {ex.Message}");
    }
})
.WithName("TestHttpCommunication")
.WithOpenApi();

///
///////////////////////////

///////////////ENDPoints End

// Health check endpoint
app.MapHealthChecks("/health");

app.Run();

// Hacer la clase Program testeable
public partial class Program { }

// Consumer para eventos de usuario
public class UserCreatedConsumer : IConsumer<UserCreatedEvent>
{
    private readonly ILogger<UserCreatedConsumer> _logger;

    public UserCreatedConsumer(ILogger<UserCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation("🎲 ORDER SERVICE recibió UserCreated:");
        _logger.LogInformation("   UserId: {UserId}", message.UserId);
        _logger.LogInformation("   Email: {Email}", message.Email);
        _logger.LogInformation("   Número aleatorio: {Number}", message.RandomNumber);

        // Simular que si el número es par, damos descuento
        if (message.RandomNumber % 2 == 0)
        {
            _logger.LogInformation("🎉 El usuario {Email} ganó 10% de descuento!",
                message.Email);
        }

        await Task.CompletedTask;
    }
}

public static class ResultsExtensions
{
    public static IResult ServiceUnavailable(object? value = null)
    {
        return Results.Json(
            value ?? new { message = "Service temporarily unavailable" },
            statusCode: StatusCodes.Status503ServiceUnavailable
        );
    }
}

