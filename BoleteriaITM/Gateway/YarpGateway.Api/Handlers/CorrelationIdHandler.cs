namespace BoleteriaITM.Gateway.YarpGateway.Api.Handlers
{
    public class CorrelationIdHandler : DelegatingHandler
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";
        private readonly ILogger<CorrelationIdHandler> _logger;

        public CorrelationIdHandler(ILogger<CorrelationIdHandler> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Generar o recuperar Correlation ID
            var correlationId = request.Headers.Contains(CorrelationIdHeader)
                ? request.Headers.GetValues(CorrelationIdHeader).FirstOrDefault()
                : Guid.NewGuid().ToString();

            if (!request.Headers.Contains(CorrelationIdHeader))
            {
                request.Headers.Add(CorrelationIdHeader, correlationId);
            }

            _logger.LogInformation("Procesando request con Correlation ID: {CorrelationId}", correlationId);

            var response = await base.SendAsync(request, cancellationToken);

            // Agregar Correlation ID a la respuesta
            response.Headers.Add(CorrelationIdHeader, correlationId);

            return response;
        }
    }
}
