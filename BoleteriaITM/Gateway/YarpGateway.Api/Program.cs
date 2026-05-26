using Serilog;
using Yarp.ReverseProxy.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Polly.CircuitBreaker;
using Polly;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BoleteriaITM.Gateway.YarpGateway.Api.Handlers;
using BoleteriaITM.Gateway.YarpGateway.Api.Middleware;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Agregar Serilog
    builder.Host.UseSerilog();

    // Configuración de autenticación JWT
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var key = Encoding.UTF8.GetBytes("your-secret-key-min-32-characters-long-please");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = jwtSettings.GetValue<bool>("ValidateIssuer"),
                ValidIssuer = jwtSettings.GetValue<string>("Authority"),
                ValidateAudience = jwtSettings.GetValue<bool>("ValidateAudience"),
                ValidAudience = jwtSettings.GetValue<string>("Audience"),
                ValidateLifetime = jwtSettings.GetValue<bool>("ValidateLifetime"),
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Log.Warning("JWT authentication failed: {Message}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var userId = context.Principal?.FindFirst("sub")?.Value;
                    Log.Information("Token validado para usuario: {UserId}", userId);
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    // Configurar CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Agregar handlers HTTP
    builder.Services.AddScoped<CorrelationIdHandler>();
    builder.Services.AddScoped<JwtValidationHandler>();

    builder.Services.AddHttpClient<HttpClient>()
        .AddHttpMessageHandler<CorrelationIdHandler>()
        .AddHttpMessageHandler<JwtValidationHandler>();

    // Configurar Polly - Circuit Breaker
    var retryPolicy = Policy
        .Handle<HttpRequestException>()
        .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                Log.Warning("Reintentando solicitud (intento {RetryCount}) después de {Delay}ms", retryCount, timespan.TotalMilliseconds);
            });

    var circuitBreakerPolicy = Policy
        .Handle<HttpRequestException>()
        .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, timespan) =>
            {
                Log.Error("Circuit breaker abierto por {Duration}s", timespan.TotalSeconds);
            },
            onReset: () =>
            {
                Log.Information("Circuit breaker reseteado");
            });

    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    // Health checks
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    // Middleware
    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    // Rate Limiting Middleware
    app.UseMiddleware<RateLimitingMiddleware>();

    // Health check endpoint
    app.MapHealthChecks("/health");

    // YARP routes
    app.MapReverseProxy();

    // Endpoint para generar JWT (solo para desarrollo)
    app.MapPost("/auth/token", (IConfiguration config) =>
    {
        var key = Encoding.UTF8.GetBytes("your-secret-key-min-32-characters-long-please");
        var signingKey = new SymmetricSecurityKey(key);
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("email", "usuario@ejemplo.com"),
            new Claim("role", "customer")
        };

        var token = new JwtSecurityToken(
            issuer: "https://localhost:5000",
            audience: "boletera-api",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: signingCredentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.WriteToken(token);

        return Results.Ok(new { token = jwtToken, expiresIn = 3600 });
    }).WithName("GenerateToken");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicación terminó inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}

// Para que funcione con minimal APIs
public partial class Program { }
