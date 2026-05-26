# 🎯 REFERENCIA RÁPIDA - YarpGateway.Api

## ⚡ Commands Essenciales

```powershell
# BUILD
dotnet build                              # Debug
dotnet build --configuration Release      # Release

# RUN
dotnet run                                # Con default settings
dotnet run --urls "https://localhost:6000" # Puerto custom

# CLEAN
dotnet clean                              # Limpiar bin/obj

# TESTS
.\test-gateway.ps1                        # Suite de pruebas

# RESTORE
dotnet restore                            # Restaurar NuGet packages
```

---

## 🔌 ENDPOINTS

### Sin Autenticación
```
GET  https://localhost:5000/health              200 OK
POST https://localhost:5000/auth/token          200 + Token
```

### Con JWT
```
GET  https://localhost:5000/api/orders          → Order.Api:5001
GET  https://localhost:5000/api/inventory       → Inventory.Api:5002
GET  https://localhost:5000/api/prices          → Price.Api:5003
GET  https://localhost:5000/api/search          → Search.Api:5004
```

---

## 🔑 JWT Token

**Generar**:
```powershell
$response = Invoke-RestMethod https://localhost:5000/auth/token -Method Post -SkipCertificateCheck
$token = $response.token
```

**Usar**:
```powershell
Invoke-RestMethod https://localhost:5000/api/orders `
  -Headers @{Authorization="Bearer $token"} `
  -SkipCertificateCheck
```

**Propiedades**:
- Issuer: `https://localhost:5000`
- Audience: `boletera-api`
- Algorithm: `HS256`
- Expiration: 1 hora
- Claims: `sub`, `email`, `role`

---

## 📝 LOGS

**Ubicación**: `logs/gateway-YYYY-MM-DD.txt`

**Ver en tiempo real** (PowerShell):
```powershell
Get-Content logs/gateway-*.txt -Wait -Tail 10
```

**Formato**:
```
[2025-01-15 14:32:15.123 +00:00] [INF] [SourceContext] Message
```

---

## 🔧 CONFIGURACIÓN

**archivo**: `appsettings.json`

```json
// JWT Settings
"JwtSettings": {
  "Authority": "https://localhost:5001",
  "Audience": "boletera-api",
  "ValidateAudience": true,
  "ValidateIssuer": true,
  "ValidateLifetime": true
}

// Rate Limiting
"RateLimiting": {
  "PermitLimit": 100,              // máximo requests
  "WindowSizeSeconds": 60          // ventana de tiempo
}

// YARP Routes (ver appsettings.json completo)
"ReverseProxy": {
  "Routes": [...],
  "Clusters": [...]
}
```

---

## 🗂️ ESTRUCTURA ARCHIVOS

```
YarpGateway.Api/
├── Program.cs                    ← Main entry point
├── YarpGateway.Api.csproj       ← NuGet packages
├── appsettings.json             ← Configuración base
├── appsettings.Development.json ← Dev config
│
├── Handlers/
│   ├── JwtValidationHandler.cs   ← JWT validation
│   └── CorrelationIdHandler.cs   ← Traceability
│
├── Middleware/
│   └── RateLimitingMiddleware.cs ← Rate limiting
│
├── Extensions/
│   └── GatewayServiceExtensions.cs ← DI helpers
│
├── Properties/
│   └── launchSettings.json       ← Run settings
│
├── README.md                     ← Full docs
├── test-gateway.ps1              ← Test suite
└── .gitignore
```

---

## 🚨 TROUBLESHOOTING

| Error | Solución |
|-------|----------|
| Port 5000 occupied | `dotnet run --urls "https://localhost:6000"` |
| Invalid certificate | Use `-SkipCertificateCheck` en PowerShell |
| Rate limit (429) | Esperar 60 segundos o cambiar `PermitLimit` |
| JWT invalid | Generar nuevo token con `/auth/token` |
| Unauthorized (401) | Token no enviado o inválido |
| Service unavailable | Backend service no corriendo (OK para dev) |

---

## 📊 CLASE: JwtValidationHandler

```csharp
// Ubicación: Handlers/JwtValidationHandler.cs
public class JwtValidationHandler : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		// 1. Extrae token del header Authorization
		// 2. Valida firma, issuer, audience, lifetime
		// 3. Retorna 401 si inválido
		// 4. Propaga token al siguiente handler si válido
	}
}
```

---

## 📊 CLASE: CorrelationIdHandler

```csharp
// Ubicación: Handlers/CorrelationIdHandler.cs
public class CorrelationIdHandler : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		// 1. Genera GUID nuevo si no existe X-Correlation-ID
		// 2. Inyecta en headers del request
		// 3. Agrega a response headers
		// 4. Logs incluyen Correlation ID
	}
}
```

---

## 📊 CLASE: RateLimitingMiddleware

```csharp
// Ubicación: Middleware/RateLimitingMiddleware.cs
public class RateLimitingMiddleware
{
	// Token bucket pattern (in-memory)
	// - 100 requests máximo por minuto
	// - Identifica por usuario (sub claim) o IP
	// - Retorna 429 si limite excedido
	// - Agregar header X-RateLimit-Remaining
}
```

---

## 🏗️ MIDDLEWARE PIPELINE

```
Request
  ↓
UseSerilogRequestLogging()  ← Logs request
  ↓
UseHttpsRedirection()       ← Redirige a HTTPS
  ↓
UseCors()                   ← CORS policy
  ↓
UseAuthentication()         ← JWT validation
  ↓
UseAuthorization()          ← Authorization checks
  ↓
RateLimitingMiddleware      ← 100 req/min limit
  ↓
MapHealthChecks()           ← /health endpoint
  ↓
MapReverseProxy()           ← YARP routing
  ↓
Response
```

---

## 🧠 FLUJO REQUEST

```
1. Cliente →  POST https://localhost:5000/auth/token
2. ↓ GenerateToken endpoint
3. ↓ Crea JWT con claims (sub, email, role)
4. ← Retorna token

---

5. Cliente → GET https://localhost:5000/api/orders
			+ Authorization: Bearer <token>
			+ X-Correlation-ID: uuid

6. ↓ CorrelationIdHandler
   ├─ Si no existe X-Correlation-ID → genera GUID
   └─ Inyecta en headers

7. ↓ JwtValidationHandler
   ├─ Extrae token del header Authorization
   ├─ Valida firma HS256
   ├─ Valida issuer, audience, lifetime
   └─ Si OK → continua; si NO → 401 Unauthorized

8. ↓ RateLimitingMiddleware
   ├─ Identifica usuario desde "sub" claim
   ├─ Revisa contador de requests (ventana 60s)
   ├─ Si < 100 → continua; si > 100 → 429 Too Many Requests
   └─ Agrega header X-RateLimit-Remaining

9. ↓ YARP ReverseProxy
   ├─ Revisa ruta: /api/orders/* → order-cluster
   ├─ Enruta a: http://localhost:5001/api/orders
   ├─ Mantiene todos los headers incluido Correlation-ID
   └─ Propaga token al siguiente servicio

10. ↓ Order.Api procesa request
	└─ Logs incluyen Correlation-ID para trazabilidad

11. ← Response retorna a través del gateway
	├─ Mantiene Correlation-ID en headers
	└─ Serilog registra duración total

12. ← Cliente recibe response + X-Correlation-ID header
```

---

## 📚 ARCHIVOS DE REFERENCIA

| Archivo | Uso |
|---------|-----|
| `Program.cs` | Main configuration |
| `appsettings.json` | YARP routes, JWT settings |
| `appsettings.Development.json` | Debug logging |
| `test-gateway.ps1` | Automated testing |
| `README.md` | Full documentation |

---

## 🎓 CONCEPTOS CLAVE

- **YARP**: Yet Another Reverse Proxy (Microsoft)
- **JWT**: JSON Web Token (autenticación stateless)
- **Correlation ID**: GUID para trazabilidad end-to-end
- **Rate Limiting**: Protección contra abuse (token bucket)
- **Middleware**: Pipeline de procesamiento de requests
- **Handlers**: Interceptores de requests (delegating handlers)
- **Health Checks**: Endpoints para orquestadores (Kubernetes)

---

## ✅ VERIFICACIÓN RÁPIDA

```powershell
# 1. ¿Compila?
dotnet build                     # ✅ Should be successful

# 2. ¿Ejecuta?
dotnet run                       # ✅ Should start on port 5000

# 3. ¿Health OK?
Invoke-RestMethod https://localhost:5000/health -SkipCertificateCheck
# Response: Healthy ✅

# 4. ¿Genera token?
$t = (Invoke-RestMethod https://localhost:5000/auth/token -Method Post -SkipCertificateCheck).token
# ✅ Token generado

# 5. ¿Valida JWT?
Invoke-RestMethod https://localhost:5000/api/orders -Headers @{Authorization="Bearer $t"} -SkipCertificateCheck
# ✅ Forwarded to Order.Api (o error 503 si no existe)
```

---

**FASE 1**: ✅ COMPLETADA
**Próximo**: FASE 2 - Microservicios
**Tiempo estimado**: 4 semanas
