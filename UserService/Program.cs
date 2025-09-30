using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ECommerce.Common.DTOs;
using ECommerce.Common.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using UserService.Services;
using UserService.Services.Data;
using BC = BCrypt.Net.BCrypt;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
builder.ConfigureSerilog("UserService");

//Configurar JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? "SuperSecretKey123!@#ForDevelopmentOnly");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(secretKey)
        };
    });

builder.Services.AddAuthorization();

// Agregar servicios
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "User Service API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
});

// Configurar Entity Framework
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Registrar servicios
builder.Services.AddScoped<IUserService, UserECService>();
builder.Services.AddScoped<ITokenService, TokenService>();
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
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Aplicar migraciones automáticamente
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    dbContext.Database.Migrate();

    // Seed data si está vacío
    if (!dbContext.Users.Any())
    {
        DataSeeder.SeedUsers(dbContext);
    }
}
///////////////////////////////con JWT/////////////////////////////////////
#region SIN JWT
///////////////////////////////SIN JWT/////////////////////////////////////
//// Configurar Serilog
//Log.Logger = new LoggerConfiguration()
//    .MinimumLevel.Information()
//    .WriteTo.Console(outputTemplate:
//        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
//    .Enrich.FromLogContext()
//    .CreateLogger();

//var builder = WebApplication.CreateBuilder(args);

//// Usar Serilog
//builder.Host.UseSerilog();

//// Agregar servicios
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen(c =>
//{
//    c.SwaggerDoc("v1", new()
//    {
//        Title = "User Service API",
//        Version = "v1",
//        Description = "Microservicio de Gestión de Usuarios - TAREA 1"
//    });
//});

//// Configurar Entity Framework
//builder.Services.AddDbContext<UserDbContext>(options =>
//    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//// Registrar servicios
//builder.Services.AddScoped<IUserService, UserService.Services.UserECService>();

//// Registrar validadores cuando los implementen
//builder.Services.AddValidatorsFromAssemblyContaining<Program>();

//// Health checks
//builder.Services.AddHealthChecks()
//    .AddSqlServer(
//        builder.Configuration.GetConnectionString("DefaultConnection") ?? "",
//        name: "database");

//// CORS
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowAll", policy =>
//    {
//        policy.AllowAnyOrigin()
//              .AllowAnyMethod()
//              .AllowAnyHeader();
//    });
//});

//var app = builder.Build();

//// Configurar el pipeline
//app.UseSerilogRequestLogging();
//app.UseCors("AllowAll");

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI(c =>
//    {
//        c.SwaggerEndpoint("/swagger/v1/swagger.json", "User Service API v1");
//        c.RoutePrefix = string.Empty;
//    });
//}

//// Aplicar migraciones automáticamente
//using (var scope = app.Services.CreateScope())
//{
//    var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
//    dbContext.Database.Migrate();

//    // Seed data si está vacío
//    if (!dbContext.Users.Any())
//    {
//        DataSeeder.SeedUsers(dbContext);
//    }
//}
#endregion SIN JWT
///////////////////////////////SIN JWT/////////////////////////////////////

// ============================================
// ENDPOINTS
// ============================================

// POST /api/auth/register - Registro de usuario
app.MapPost("/api/auth/register", async (
    RegisterDto dto,
    IUserService userService,
    IValidator<RegisterDto> validator) =>
{
    var validationResult = await validator.ValidateAsync(dto);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(ApiResponse<UserDto>.Fail(
            "Validation fallida",
            validationResult.Errors.Select(e => e.ErrorMessage).ToList()));
    }

    try
    {
        var user = await userService.RegisterAsync(dto);
        return Results.Created($"/api/users/{user.Id}",
            ApiResponse<UserDto>.Ok(user, "User registrado ok"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error al registrar el usuario");
        return Results.Problem("Un error occurrio durante el registro");
    }
})
.WithName("Register")
.Produces<ApiResponse<UserDto>>(201)
.Produces(400)
.AllowAnonymous()
.WithOpenApi();

// POST /api/auth/login - Login de usuario
app.MapPost("/api/auth/login", async (
    LoginDto dto,
    IUserService userService,
    ITokenService tokenService,
    IValidator<LoginDto> validator) =>
{
    var validationResult = await validator.ValidateAsync(dto);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(ApiResponse<LoginResponseDto>.Fail(
            "Validation fallida",
            validationResult.Errors.Select(e => e.ErrorMessage).ToList()));
    }

    try
    {
        var user = await userService.ValidateCredentialsAsync(dto.Email, dto.Password);
        if (user == null)
        {
            return Results.Unauthorized();// (ApiResponse<LoginResponseDto>.Fail("Credenciales invalidas"));
        }

        var token = tokenService.GenerateToken(user);
        var response = new LoginResponseDto
        {
            User = user,
            Token = token,
            ExpiresIn = 3600 // 1 hora
        };

        return Results.Ok(ApiResponse<LoginResponseDto>.Ok(response, "Login ok"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error en el login");
        return Results.Problem("Un error occurrio mientras se hacia login");
    }
})
.WithName("Login")
.Produces<ApiResponse<LoginResponseDto>>(200)
.Produces(401)
.AllowAnonymous()
.WithOpenApi();

// GET /api/users - Obtener todos los usuarios (Admin only)
app.MapGet("/api/users", async (IUserService userService, ClaimsPrincipal user) =>
{
    try
    {
        // Verificar si es admin
        if (!user.IsInRole("Admin"))
        {
            return Results.Forbid();
        }

        var users = await userService.GetAllUsersAsync();
        return Results.Ok(ApiResponse<List<UserDto>>.Ok(users));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error obteniendo usuarios");
        return Results.Problem("Un error occurrio mientras se obtenia los usuarios");
    }
})
.RequireAuthorization()
.WithName("GetUsers")
.Produces<ApiResponse<List<UserDto>>>(200)
.Produces(403)
.WithOpenApi();

// GET /api/users/{id} - Obtener usuario por ID
app.MapGet("/api/users/{id:int}", async (int id, IUserService userService) =>
{
    try
    {
        var user = await userService.GetUserByIdAsync(id);
        if (user == null)
            return Results.NotFound(ApiResponse<UserDto>.Fail($"Usuario con ID {id} no se encontro"));

        return Results.Ok(ApiResponse<UserDto>.Ok(user));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error obteniendo usuario {UserId}", id);
        return Results.Problem($"Un error occurrio mientras se consultaba al usuario {id}");
    }
})
.WithName("GetUserById")
.Produces<ApiResponse<UserDto>>(200)
.Produces(404)
.WithOpenApi();

// PUT /api/users/{id}/validate-email - Validar email GDPR
app.MapPut("/api/users/{id:int}/validate-email", async (
    int id,
    IUserService userService) =>
{
    try
    {
        var isValid = await userService.ValidateEmailGDPRAsync(id);
        var message = isValid
            ? "Email cumple con GDPR (EU domain)"
            : "Email no cumple con GDPR (non-EU domain)";

        return Results.Ok(ApiResponse<bool>.Ok(isValid, message));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error validadando el correo del usuario {UserId}", id);
        return Results.Problem($"Un error occurrio mientras se validaba el correo del usuario {id}");
    }
})
.WithName("ValidateEmailGDPR")
.Produces<ApiResponse<bool>>(200)
.WithOpenApi();

// GET /api/users/{id}/discount - Obtener descuento premium
app.MapGet("/api/users/{id:int}/discount", async (
    int id,
    IUserService userService) =>
{
    try
    {
        var discount = await userService.GetUserDiscountAsync(id);
        if (discount == null)
            return Results.NotFound(ApiResponse<decimal>.Fail($"Usuario con ID {id} no se encontro"));

        return Results.Ok(ApiResponse<decimal>.Ok(discount.Value,
            $"Descuento para el usuario: {discount.Value:P}"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error al obtener el descuento del usuario {UserId}", id);
        return Results.Problem($"Un error occurrio mientras se obtenia el descuento para el usuario {id}");
    }
})
.WithName("GetUserDiscount")
.Produces<ApiResponse<decimal>>(200)
.Produces(404)
.WithOpenApi();

// Health check endpoint
app.MapHealthChecks("/health");

// Endpoint de información
app.MapGet("/", () => new
{
    Service = "Servicio de usuarios",
    Version = "1.0.0",
    Status = "Running",
    Endpoints = new[]
    {
        "POST /api/auth/register",
        "POST /api/auth/login",
        "GET /api/users",
        "PUT /api/users/{id}/validate-email",
        "GET /api/users/{id}",
        "GET /api/users/{id}/discount",
        "GET /health"
    },
    Documentation = "/swagger"
});

Console.WriteLine("🚀 Sistema E-Commerce Microservicio de Usuario iniciado");
Console.WriteLine("📍 Swagger disponible en: https://localhost:7002/swagger");
app.Run();

// Hacer la clase Program testeable
public partial class Program { }