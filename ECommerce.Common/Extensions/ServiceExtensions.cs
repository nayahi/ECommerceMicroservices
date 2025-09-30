using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Polly;
using Polly.Extensions.Http;

namespace ECommerce.Common.Extensions
{
    public static class ServiceExtensions
    {
        // Configuración de Serilog para todos los servicios
        public static void ConfigureSerilog(this WebApplicationBuilder builder, string serviceName)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] {Message:lj}{NewLine}{Exception}") // This requires Serilog.Sinks.Console
                .Enrich.WithProperty("ServiceName", serviceName)
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
                .CreateLogger();

            builder.Host.UseSerilog();
        }

        // Configuración de HttpClient con Polly para resiliencia
        public static IHttpClientBuilder AddResilientHttpClient(
            this IServiceCollection services,
            string name,
            string baseUrl)
        {
            return services.AddHttpClient(name, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());
        }

        // Política de reintentos: 3 intentos con backoff exponencial
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        Log.Warning("Retry {RetryCount} after {Timespan}s", retryCount, timespan.TotalSeconds);
                    });
        }

        // Circuit Breaker: Se abre después de 3 fallos consecutivos
        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    3,
                    TimeSpan.FromSeconds(30),
                    onBreak: (result, timespan) =>
                    {
                        Log.Error("Circuit breaker opened for {Timespan}s", timespan.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        Log.Information("Circuit breaker reset");
                    });
        }

        // Middleware de correlación para trazabilidad
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                if (!context.Request.Headers.ContainsKey("X-Correlation-Id"))
                {
                    context.Request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
                }

                context.Response.Headers.Add("X-Correlation-Id",
                    context.Request.Headers["X-Correlation-Id"].ToString());

                await next();
            });
        }

        // Health checks estándar
        public static void AddStandardHealthChecks(this IServiceCollection services, string connectionString)
        {
            services.AddHealthChecks()
                .AddSqlServer(connectionString, name: "database", tags: new[] { "db", "sql" });
        }
    }
}