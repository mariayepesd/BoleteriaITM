using BoleteriaITM.Gateway.YarpGateway.Api.Handlers;

namespace BoleteriaITM.Gateway.YarpGateway.Api.Extensions
{
    public static class GatewayServiceExtensions
    {
        /// <summary>
        /// Registra los handlers personalizados del gateway
        /// </summary>
        public static IServiceCollection AddGatewayHandlers(this IServiceCollection services)
        {
            services.AddScoped<CorrelationIdHandler>();
            services.AddScoped<JwtValidationHandler>();
            return services;
        }

        /// <summary>
        /// Configura CORS para desarrollo
        /// </summary>
        public static IServiceCollection AddGatewayCors(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    // En desarrollo: permitir cualquier origen
                    if (configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    }
                    else
                    {
                        // En producción: especificar orígenes permitidos
                        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "https://localhost:5000" };
                        policy.WithOrigins(allowedOrigins)
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    }
                });
            });

            return services;
        }

        /// <summary>
        /// Agrega logging estructurado al HttpClient
        /// </summary>
        public static IHttpClientBuilder AddGatewayHttpClient(this IServiceCollection services, string name = "")
        {
            var clientBuilder = services.AddHttpClient(name);
            clientBuilder.AddHttpMessageHandler<CorrelationIdHandler>();
            clientBuilder.AddHttpMessageHandler<JwtValidationHandler>();
            return clientBuilder;
        }
    }
}
