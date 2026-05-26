# 🚀 GUÍA RÁPIDA - YarpGateway.Api

## ⚡ Inicio Rápido (1 minuto)

### En Visual Studio
1. Abre la solución `BoleteriaITM.sln`
2. Click derecho en `YarpGateway.Api` → "Set as Startup Project"
3. Presiona `F5`
4. El gateway inicia en `https://localhost:5000`

### En PowerShell
```powershell
cd C:\Users\MajoY\source\repos\BoleteriaITM\src\Gateway\YarpGateway.Api
dotnet run
```

---

## 🧪 Probar el Gateway (5 minutos)

```powershell
# 1. Ejecutar script de pruebas completo
cd C:\Users\MajoY\source\repos\BoleteriaITM\src\Gateway\YarpGateway.Api
.\test-gateway.ps1
```

O manualmente:

```powershell
# 2. Verificar health
Invoke-RestMethod https://localhost:5000/health -SkipCertificateCheck

# 3. Generar token
$token = (Invoke-RestMethod https://localhost:5000/auth/token -Method Post -SkipCertificateCheck).token
Write-Host "Token: $token"

# 4. Usar token (simula llamada a Order.Api)
Invoke-RestMethod https://localhost:5000/api/orders `
  -Headers @{Authorization="Bearer $token"} `
  -SkipCertificateCheck
```

---

## 📊 Endpoints Disponibles

| Método | Endpoint | Autenticación | Descripción |
|--------|----------|---------------|-------------|
| `GET` | `/health` | ❌ No | Health check |
| `POST` | `/auth/token` | ❌ No | Generar JWT |
| `GET` | `/api/orders/*` | ✅ JWT | Enrutado a Order.Api:5001 |
| `GET` | `/api/inventory/*` | ✅ JWT | Enrutado a Inventory.Api:5002 |
| `GET` | `/api/prices/*` | ✅ JWT | Enrutado a Price.Api:5003 |
| `GET` | `/api/search/*` | ✅ JWT | Enrutado a Search.Api:5004 |

---

## 📁 Archivos Importantes

| Archivo | Propósito |
|---------|----------|
| `Program.cs` | Configuración principal |
| `appsettings.json` | Config base + rutas YARP |
| `appsettings.Development.json` | Config DEBUG |
| `Handlers/JwtValidationHandler.cs` | Validación JWT |
| `Handlers/CorrelationIdHandler.cs` | Trazabilidad |
| `Middleware/RateLimitingMiddleware.cs` | Rate limiting |
| `test-gateway.ps1` | Script pruebas |
| `logs/gateway-*.txt` | Archivos de log |

---

## 🔑 Configuración JWT

**Secret Key (Desarrollo)**:
```
your-secret-key-min-32-characters-long-please
```

**Token Claims**:
```json
{
  "sub": "user-123",
  "email": "usuario@ejemplo.com",
  "role": "customer",
  "iss": "https://localhost:5000",
  "aud": "boletera-api",
  "exp": "2025-...", // +1 hora
  "iat": "2025-..."
}
```

---

## 📝 Estructura de Logs

**Ubicación**: `src/Gateway/YarpGateway.Api/logs/gateway-YYYY-MM-DD.txt`

**Formato**:
```
[2025-01-15 14:32:15.123 +00:00] [INF] [YarpGateway.Api.Handlers] Mensaje descriptivo
```

**Eventos principales**:
- ✅ Startup del gateway
- ✅ Requests con Correlation ID
- ✅ JWT validation (success/failure)
- ✅ Rate limiting triggered
- ✅ Forwarding a servicios

---

## ⚠️ Problemas Comunes

### Error: "No se encontró puerto 5000"
```powershell
# Puerto ocupado. Usa otro:
dotnet run --urls="https://localhost:5555"
```

### Error: "Certificado autofirmado no válido"
```powershell
# Agregar -SkipCertificateCheck en Invoke-RestMethod
Invoke-RestMethod ... -SkipCertificateCheck
```

### Error: "Rate limit exceeded (429)"
```powershell
# Esperar 60 segundos o cambiar límite en appsettings.json
# "PermitLimit": 100,
# "WindowSizeSeconds": 60
```

---

## 🔐 Seguridad en Producción

- ❌ NO: Usar secret key en texto plano
- ✅ SI: Usar Azure Key Vault o AWS Secrets Manager
- ✅ SI: Habilitar mTLS entre servicios
- ✅ SI: Limitar CORS a dominios específicos
- ✅ SI: Usar certificados SSL válidos
- ✅ SI: Implementar WAF (Web Application Firewall)

---

## 📚 Documentación Completa

Ver: `src/Gateway/YarpGateway.Api/README.md`

---

**¿Listo para continuar con FASE 2?** → Crear microservicios backend ⚙️
