using CatalogService.Data;
using CatalogService.DTOs;
using CatalogService.Models;
using CatalogService.Services;
using ECommerce.Common.DTOs;
using FluentValidation;
using MassTransit.Futures.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using static MassTransit.ValidationResultExtensions;
using ECommerce.Common.Extensions;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
// Configurar Serilog
builder.ConfigureSerilog("ProductService");

// Usar Serilog
builder.Host.UseSerilog();

// Agregar servicios
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Catalog Service API",
        Version = "v1",
        Description = "Microservicio de Catálogo de Productos",
        Contact = new OpenApiContact
        {
            Name = "Equipo de ECommerce Monolítico",
            Email = "soporte@ECommerceMonolítico.com"
        }
    });
});

// Configurar Entity Framework con SQL Server
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Registrar servicios
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Health checks
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? "",
        name: "database",
        tags: new[] { "db", "sql", "sqlserver" });

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

// Configurar el pipeline
app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog Service API v1");
        c.RoutePrefix = string.Empty; // Swagger en la raíz
    });
}

// Aplicar migraciones automáticamente al iniciar
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        dbContext.Database.Migrate();
        Log.Information("Database migrated successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error migrating database");
    }
}

// ============ ENDPOINTS ============

// GET / api / products - Obtener todos los productos
app.MapGet("/api/products", async (
    IProductService catalogService,
    int pageNumber = 1,
    int pageSize = 10,
    string ? category = null,
    decimal ? minPrice = null,
    decimal ? maxPrice = null) =>
{
    try
    {
        var result = await catalogService.GetProductsAsync(
            pageNumber, pageSize, category, minPrice, maxPrice);
return Results.Ok(ApiResponse<PagedResponse<ProductDto>>.Ok(result));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error al conseguir los productos");
return Results.Problem("Un error occurrio mientras se obtenian los productos");
    }
})
.WithName("GetProducts")
.Produces<ApiResponse<PagedResponse<ProductDto>>>(200)
.WithOpenApi();

// GET /api/products/{id} - Obtener producto por ID
app.MapGet("/api/products/{id:int}", async (int id, IProductService catalogService) =>
{
    try
    {
        var product = await catalogService.GetProductByIdAsync(id);
        if (product == null)
            return Results.NotFound(ApiResponse<ProductDto>.Fail($"Producto con ID {id} no se encontro"));

        return Results.Ok(ApiResponse<ProductDto>.Ok(product));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error obteniendo el producto {ProductId}", id);
        return Results.Problem($"Un error occurrio mientras se obtenia el producto {id}");
    }
})
.WithName("GetProductById")
.Produces<ApiResponse<ProductDto>>(200)
.Produces(404)
.WithOpenApi();

// POST /api/products - Crear nuevo producto
app.MapPost("/api/products", async (
    CreateProductDto dto,
    IProductService catalogService,
    IValidator<CreateProductDto> validator) =>
{
    var validationResult = await validator.ValidateAsync(dto);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(ApiResponse<ProductDto>.Fail(
            "Validation fallida",
            validationResult.Errors.Select(e => e.ErrorMessage).ToList()));
    }

    try
    {
        var product = await catalogService.CreateProductAsync(dto);
        return Results.Created($"/api/products/{product.Id}",
            ApiResponse<ProductDto>.Ok(product, "Producto creado ok"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error creando el producto");
        return Results.Problem("Un error occurrio mientras se creaba el producto");
    }
})
.WithName("CreateProduct")
.Produces<ApiResponse<ProductDto>>(201)
.Produces(400)
.WithOpenApi();

// PUT /api/products/{id} - Actualizar producto
app.MapPut("/api/products/{id:int}", async (
    int id,
    UpdateProductDto dto,
    IProductService catalogService,
    IValidator<UpdateProductDto> validator) =>
{
    var validationResult = await validator.ValidateAsync(dto);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(ApiResponse<ProductDto>.Fail(
            "Validacion fallida",
            validationResult.Errors.Select(e => e.ErrorMessage).ToList()));
    }

    try
    {
        var product = await catalogService.UpdateProductAsync(id, dto);
        if (product == null)
            return Results.NotFound(ApiResponse<ProductDto>.Fail($"Producto con ID {id} no se encontro"));

        return Results.Ok(ApiResponse<ProductDto>.Ok(product, "Producto actualizado ok"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error actualizando producto {ProductId}", id);
        return Results.Problem($"Un error occurrio mientras se actualizaba el producto {id}");
    }
})
.WithName("UpdateProduct")
.Produces<ApiResponse<ProductDto>>(200)
.Produces(404)
.WithOpenApi();

// DELETE /api/products/{id} - Eliminar producto
app.MapDelete("/api/products/{id:int}", async (int id, IProductService catalogService) =>
{
    try
    {
        var result = await catalogService.DeleteProductAsync(id);
        if (!result)
            return Results.NotFound(ApiResponse<bool>.Fail($"Producto con ID {id} no se encontro"));

        return Results.Ok(ApiResponse<bool>.Ok(true, "Product borrado ok"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error borrando el producto {ProductId}", id);
        return Results.Problem($"Un error occurrio mientras se borraba el producto {id}");
    }
})
.WithName("DeleteProduct")
.Produces<ApiResponse<bool>>(200)
.Produces(404)
.WithOpenApi();

// GET /api/products/search - Buscar productos
app.MapGet("/api/products/search", async (
    string query,
    IProductService catalogService,
    int pageNumber = 1,
    int pageSize = 10) =>
{
    try
    {
        var result = await catalogService.SearchProductsAsync(query, pageNumber, pageSize);
        return Results.Ok(ApiResponse<PagedResponse<ProductDto>>.Ok(result));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error buscando productos con la consulta: {Query}", query);
        return Results.Problem("Un error occurrio mientras se buscaban productos");
    }
})
.WithName("SearchProducts")
.Produces<ApiResponse<PagedResponse<ProductDto>>>(200)
.WithOpenApi();

// GET /api/products/{id}/stock - Verificar stock
app.MapGet("/api/products/{id:int}/stock", async (int id, IProductService catalogService) =>
{
    try
    {
        var stock = await catalogService.GetProductStockAsync(id);
        if (stock == null)
            return Results.NotFound(ApiResponse<int>.Fail($"Producto con ID {id} no se encontro"));

        return Results.Ok(ApiResponse<int>.Ok(stock.Value));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error obteniendo el stock del producto {ProductId}", id);
        return Results.Problem($"Un error occurrio mientras se obtenia el stock del producto {id}");
    }
})
.WithName("GetProductStock")
.Produces<ApiResponse<int>>(200)
.Produces(404)
.WithOpenApi();

/*
// GET /api/products - Obtener todos los productos
app.MapGet("/api/products", async (IProductService productService) =>
{
    try
    {
        var products = await productService.GetAllProductsAsync();
        return Results.Ok(ApiResponse<List<ProductDto>>.Ok(products, "Products retrieved successfully"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error getting products");
        return Results.Problem("An error occurred while fetching products");
    }
})
.WithName("GetProducts")
.WithOpenApi()
.Produces<ApiResponse<List<ProductDto>>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status500InternalServerError);

// GET /api/products/{id} - Obtener producto por ID
app.MapGet("/api/products/{id:int}", async (int id, IProductService productService) =>
{
    try
    {
        var product = await productService.GetProductByIdAsync(id);
        if (product == null)
            return Results.NotFound(ApiResponse<ProductDto>.Fail($"Product with ID {id} not found"));

        return Results.Ok(ApiResponse<ProductDto>.Ok(product));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error getting product {ProductId}", id);
        return Results.Problem($"An error occurred while fetching product {id}");
    }
})
.WithName("GetProductById")
.WithOpenApi()
.Produces<ApiResponse<ProductDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// POST /api/products - Crear nuevo producto
app.MapPost("/api/products", async (CreateProductDto dto, IProductService productService) =>
{
    try
    {
        // Validación básica
        if (string.IsNullOrEmpty(dto.Name))
            return Results.BadRequest(ApiResponse<ProductDto>.Fail("Product name is required"));

        if (dto.Price <= 0)
            return Results.BadRequest(ApiResponse<ProductDto>.Fail("Price must be greater than 0"));

        var product = await productService.CreateProductAsync(dto);
        return Results.Created($"/api/products/{product.Id}",
            ApiResponse<ProductDto>.Ok(product, "Product created successfully"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error creating product");
        return Results.Problem("An error occurred while creating the product");
    }
})
.WithName("CreateProduct")
.WithOpenApi()
.Produces<ApiResponse<ProductDto>>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest);

// PUT /api/products/{id} - Actualizar producto
app.MapPut("/api/products/{id:int}", async (int id, UpdateProductDto dto, IProductService productService) =>
{
    try
    {
        var product = await productService.UpdateProductAsync(id, dto);
        if (product == null)
            return Results.NotFound(ApiResponse<ProductDto>.Fail($"Product with ID {id} not found"));

        return Results.Ok(ApiResponse<ProductDto>.Ok(product, "Product updated successfully"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error updating product {ProductId}", id);
        return Results.Problem($"An error occurred while updating product {id}");
    }
})
.WithName("UpdateProduct")
.WithOpenApi()
.Produces<ApiResponse<ProductDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// DELETE /api/products/{id} - Eliminar producto
app.MapDelete("/api/products/{id:int}", async (int id, IProductService productService) =>
{
    try
    {
        var result = await productService.DeleteProductAsync(id);
        if (!result)
            return Results.NotFound(ApiResponse<bool>.Fail($"Product with ID {id} not found"));

        return Results.Ok(ApiResponse<bool>.Ok(true, "Product deleted successfully"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error deleting product {ProductId}", id);
        return Results.Problem($"An error occurred while deleting product {id}");
    }
})
.WithName("DeleteProduct")
.WithOpenApi()
.Produces<ApiResponse<bool>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// GET /api/products/search?term={term} - Buscar productos --REUSE Later
app.MapGet("/api/products/search", async (string term, IProductService productService) =>
{
    try
    {
        var products = await productService.SearchProductsAsync(term);
        return Results.Ok(ApiResponse<List<ProductDto>>.Ok(products, $"Found {products.Count} products"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error searching products");
        return Results.Problem("An error occurred while searching products");
    }
})
.WithName("SearchProducts")
.WithOpenApi()
.Produces<ApiResponse<List<ProductDto>>>(StatusCodes.Status200OK);
*/

// Health check endpoint
app.MapHealthChecks("/health");

// Endpoint de información
app.MapGet("/", () => new
{
    Service = "Catalog Service",
    Version = "1.0.0",
    Status = "Running",
    Endpoints = new[]
    {
        "GET /api/products",
        "GET /api/products/{id}",
        "POST /api/products",
        "PUT /api/products/{id}",
        "DELETE /api/products/{id}",
        "get /api/products/search",
        //"GET /api/products/search?term={term}",
        "GET /api/products/{id:int}/stock",
        "GET /health"
    },
    Documentation = "/swagger"
});

Console.WriteLine("🚀 Sistema E-Commerce Microservicio Catalogo de Productos iniciado");
Console.WriteLine("📍 Swagger disponible en: https://localhost:7001/swagger");
app.Run();

// Hacer la clase Program testeable
public partial class Program { }
