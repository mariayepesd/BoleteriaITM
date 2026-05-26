namespace BoleteriaITM.Gateway.YarpGateway.Api.Middleware
{
    /// <summary>
    /// Middleware para Rate Limiting basado en IP o usuario
    /// Implementa el patrón token bucket simple
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private static readonly Dictionary<string, (DateTime ResetTime, int RequestCount)> _requestCounts = new();
        private static readonly object _lock = new object();

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Ignorar rate limiting para health checks
            if (context.Request.Path == "/health")
            {
                await _next(context);
                return;
            }

            var identifier = GetIdentifier(context);
            var (permitted, remainingRequests) = IsRequestPermitted(identifier);

            context.Response.Headers.Append("X-RateLimit-Remaining", remainingRequests.ToString());

            if (!permitted)
            {
                _logger.LogWarning("Rate limit excedido para: {Identifier}", identifier);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = "Rate limit excedido. Máximo 100 requests por minuto." });
                return;
            }

            await _next(context);
        }

        private string GetIdentifier(HttpContext context)
        {
            // Preferir usuario autenticado, sino usar IP
            var userId = context.User?.FindFirst("sub")?.Value;
            return !string.IsNullOrEmpty(userId) ? $"user:{userId}" : $"ip:{context.Connection.RemoteIpAddress}";
        }

        private (bool Permitted, int RemainingRequests) IsRequestPermitted(string identifier)
        {
            const int permitLimit = 100;
            const int windowSeconds = 60;

            lock (_lock)
            {
                var now = DateTime.UtcNow;

                if (_requestCounts.TryGetValue(identifier, out var current))
                {
                    var secondsElapsed = (now - current.ResetTime).TotalSeconds;

                    if (secondsElapsed > windowSeconds)
                    {
                        // Ventana expirada, resetear
                        _requestCounts[identifier] = (now, 1);
                        return (true, permitLimit - 1);
                    }

                    if (current.RequestCount >= permitLimit)
                    {
                        return (false, 0);
                    }

                    var newCount = current.RequestCount + 1;
                    _requestCounts[identifier] = (current.ResetTime, newCount);
                    return (true, permitLimit - newCount);
                }
                else
                {
                    // Primera solicitud
                    _requestCounts[identifier] = (now, 1);
                    return (true, permitLimit - 1);
                }
            }
        }
    }
}
