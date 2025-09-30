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

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
builder.ConfigureSerilog("OrderECService");

// Agregar servicios
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Order Service API", Version = "v1" });
});

// Configurar Entity Framework
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configurar HttpClients para comunicación con otros servicios
var serviceUrls = builder.Configuration.GetSection("ServiceUrls").Get<ServiceUrls>() ?? new ServiceUrls();
builder.Services.AddSingleton(serviceUrls);

builder.Services.AddResilientHttpClient("CatalogService", serviceUrls.CatalogService);
builder.Services.AddResilientHttpClient("UserService", serviceUrls.UserService);

// Configurar MassTransit con RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<StockReservedConsumer>();
    x.AddConsumer<PaymentProcessedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitSettings = builder.Configuration.GetSection("RabbitMQ").Get<RabbitMQSettings>() ?? new RabbitMQSettings();

        cfg.Host(rabbitSettings.Host, rabbitSettings.VirtualHost, h =>
        {
            h.Username(rabbitSettings.Username);
            h.Password(rabbitSettings.Password);
        });

        cfg.ConfigureEndpoints(context);
    });
});

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

// Health check endpoint
app.MapHealthChecks("/health");

app.Run();