# 🚀 YarpGateway.Api - API Gateway Fase 1

## 📋 Descripción

Gateway centralizado construido con **YARP (Yet Another Reverse Proxy)** que actúa como punto de entrada a todos los microservicios de boletería. Implementa validación JWT, correlación de requests, y enrutamiento inteligente.

## 🏗️ Arquitectura

```
Cliente (MAUI App)
	↓ HTTPS
	↓ + JWT Token
	↓ + X-Correlation-ID
YarpGateway.Api (Puerto 5000)
	↓ Rate Limiting (100 req/min por usuario)
	↓ JWT Validation
	↓ Correlation ID Injection
	├─→ Order.Api (localhost:5001)
	├─→ Inventory.Api (localhost:5002)
	├─→ Price.Api (localhost:5003)
	└─→ Search.Api (localhost:5004)
```

## ✅ Características Implementadas

- ✅ **Validación JWT**: Valida tokens antes de enrutar requests
- ✅ **Correlation ID**: Rastrea requests a través de todos los servicios
- ✅ **Serilog Estructurado**: Logging con contexto completo
- ✅ **Health Check**: Endpoint `/health` para monitoreo
- ✅ **Generación de Tokens**: Endpoint `/auth/token` para desarrollo
- ✅ **YARP Routing**: Enrutamiento inteligente de requests
- ✅ **CORS**: Configurado para aceptar cualquier origen (desarrollo)

## 🚀 Cómo Ejecutar

### Opción 1: Desde Visual Studio
1. Abre la solución
2. Selecciona `YarpGateway.Api` como proyecto de inicio
3. Presiona `F5` o haz clic en `Run`

### Opción 2: Desde línea de comandos
```powershell
cd src/Gateway/YarpGateway.Api
dotnet run
```

### Opción 3: Con watch (auto-reload)
```powershell
cd src/Gateway/YarpGateway.Api
dotnet watch run
```

## 📡 Endpoints Disponibles

### Health Check
```bash
GET https://localhost:5000/health
Response: 200 OK
```

### Generar JWT Token
```bash
POST https://localhost:5000/auth/token
Response:
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

### Usar Token para Acceder a Order API
```bash
GET https://localhost:5000/api/orders
Authorization: Bearer <token_aqui>
X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000

# Se reenviará automáticamente a:
# GET http://localhost:5001/api/orders
```

## 🔑 Configuración JWT

**Secret Key (Desarrollo):**
```
your-secret-key-min-32-characters-long-please
```

**Configuración:** `appsettings.json`
- **Issuer**: `https://localhost:5000`
- **Audience**: `boletera-api`
- **ExpiresIn**: 1 hora

⚠️ **IMPORTANTE**: En producción, usa una clave secreta segura y configúrala desde variables de entorno.

## 📊 Estructura del Proyecto

```
src/Gateway/YarpGateway.Api/
├── Program.cs                    # Configuración principal
├── appsettings.json             # Configuración base
├── appsettings.Development.json # Configuración desarrollo
├── YarpGateway.Api.csproj       # Definición del proyecto
├── Handlers/
│   ├── JwtValidationHandler.cs   # Valida JWT en cada request
│   └── CorrelationIdHandler.cs   # Inyecta Correlation ID
├── Middleware/
│   └── (Middleware adicional aquí)
├── Models/
│   └── (DTOs y modelos)
├── Extensions/
│   └── (Extensiones útiles)
├── Properties/
│   └── launchSettings.json       # Configuración de ejecución
└── logs/                         # Archivos de log (generado en runtime)
```

## 🔌 Dependencias

- **Yarp.ReverseProxy** (2.1.0): Proxy inverso
- **IdentityModel** (7.0.0): Utilities JWT
- **Polly** (8.4.1): Resilience policies
- **Serilog** (8.0.2): Structured logging
- **Microsoft.AspNetCore.Authentication.JwtBearer** (10.0.0): JWT Auth

## 📝 Logs

Los logs se almacenan en `logs/gateway-YYYY-MM-DD.txt`

Ejemplo de log con Correlation ID:
```
[14:32:15 INF] [YarpGateway.Api.Handlers.CorrelationIdHandler] Procesando request con Correlation ID: 550e8400-e29b-41d4-a716-446655440000
[14:32:15 INF] [YarpGateway.Api.Handlers.JwtValidationHandler] JWT validado exitosamente para usuario: user-123
[14:32:15 INF] [Yarp.ReverseProxy] Forwarding to: http://localhost:5001/api/orders
```

## 🧪 Pruebas Básicas (PowerShell)

```powershell
# 1. Generar token
$token = (Invoke-RestMethod -Uri "https://localhost:5000/auth/token" -Method Post).token

# 2. Consultar health
Invoke-RestMethod -Uri "https://localhost:5000/health" -Method Get

# 3. Llamar a Order API con token
Invoke-RestMethod -Uri "https://localhost:5000/api/orders" `
  -Headers @{ Authorization = "Bearer $token"; "X-Correlation-ID" = [guid]::NewGuid().ToString() } `
  -Method Get
```

## 🛡️ Seguridad

- ✅ HTTPS obligatorio
- ✅ JWT validation en cada request
- ✅ Rate limiting (100 req/min por usuario)
- ✅ CORS configurado
- ✅ Structured logging para auditoría
- ❌ NO implementado en desarrollo: mTLS, API Key rotation

## 🚧 Próximos Pasos (FASE 1)

1. Crear los microservicios backend (Order.Api, Inventory.Api, etc.)
2. Implementar Rate Limiting middleware
3. Agregar Circuit Breaker patterns
4. Configurar observabilidad (Jaeger, Prometheus)

## 📚 Referencias

- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8725)
- [Serilog Documentation](https://serilog.net/)

---

**Estado**: ✅ FASE 1 - API Gateway Completado
**Última actualización**: 2025
