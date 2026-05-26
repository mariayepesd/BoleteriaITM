using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace BoleteriaITM.Gateway.YarpGateway.Api.Handlers
{
    public class JwtValidationHandler : DelegatingHandler
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtValidationHandler> _logger;

        public JwtValidationHandler(IConfiguration configuration, ILogger<JwtValidationHandler> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Extraer token del header
            if (!request.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                _logger.LogWarning("Authorization header no encontrado");
                return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Token no proporcionado")
                };
            }

            var token = authHeaders.FirstOrDefault()?.Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Token vacío o mal formado");
                return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Token mal formado")
                };
            }

            // Validar JWT
            try
            {
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("akLo987aWeknM_HjMae12!"));

                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = jwtSettings.GetValue<bool>("ValidateIssuer"),
                    ValidIssuer = jwtSettings.GetValue<string>("Authority"),
                    ValidateAudience = jwtSettings.GetValue<bool>("ValidateAudience"),
                    ValidAudience = jwtSettings.GetValue<string>("Audience"),
                    ValidateLifetime = jwtSettings.GetValue<bool>("ValidateLifetime"),
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                _logger.LogInformation("JWT validado exitosamente para usuario: {UserId}", principal.FindFirst("sub")?.Value);

                // Continuar con la solicitud
                return await base.SendAsync(request, cancellationToken);
            }

            catch (SecurityTokenException ex)
            {
                _logger.LogError("Error validando JWT: {Error}", ex.Message);
                return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent($"Token inválido: {ex.Message}")
                };
            }

            catch (Exception ex)
            {
                _logger.LogError("Error inesperado validando JWT: {Error}", ex.Message);
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Error procesando autenticación")
                };
            }
        }
    }
}
