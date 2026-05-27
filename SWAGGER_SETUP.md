# 🔧 Configuración de Swagger en Docker

## ✅ Cambios Realizados

### 1. **Downgrade de Swashbuckle.AspNetCore**
- Actualizado de **7.0.0** → **6.4.0** en ambos servicios
  - `InventoryService/InventoryService.csproj`
  - `OrderService/OrderService.csproj`
- **Razón**: Swashbuckle 7.0.0 tiene incompatibilidad con los ensamblados de OpenAPI en tiempo de ejecución, causando `TypeLoadException`

### 2. **Docker Compose Actualizado**
Agregados los tres servicios de API al `docker-compose.yml`:

- **InventoryService**
  - Puerto: `5273:8080` (HTTP)
  - Swagger: `http://localhost:5273/swagger/index.html`
  - Database: `FestivalInventoryDb`

- **OrderService**
  - Puerto: `5280:8080` (HTTP)
  - Swagger: `http://localhost:5280/swagger/index.html`
  - Database: `FestivalOrdersDb`

- **ApiGateway**
  - Puerto: `5000:8080` (HTTP)
  - Swagger: `http://localhost:5000/swagger/index.html`

### 3. **Variables de Entorno Configuradas**
En cada servicio se establece:
```yaml
environment:
  ASPNETCORE_ENVIRONMENT: Development  # ← Habilita Swagger
  ASPNETCORE_URLS: "http://+:8080;https://+:8443"
  ConnectionStrings__DefaultConnection: "Server=sqlserver;..."
```

### 4. **Dockerfiles Actualizados**
Se agregó la exposición del puerto HTTPS (8443) en todos los Dockerfiles:
```dockerfile
EXPOSE 8080
EXPOSE 8443
```

## 🚀 Cómo Ejecutar

```powershell
# Construir y levantar todos los contenedores
cd C:\Users\MajoY\source\repos\BoleteriaITM\
docker-compose up --build

# O ejecutar en background
docker-compose up -d --build
```

## 📖 Acceso a Swagger

Una vez que los contenedores estén corriendo:

| Servicio | URL |
|----------|-----|
| InventoryService | `http://localhost:5273/swagger/index.html` |
| OrderService | `http://localhost:5280/swagger/index.html` |
| ApiGateway | `http://localhost:5000/swagger/index.html` |

## 🔍 Verificar Estado de Contenedores

```powershell
# Listar contenedores
docker ps

# Ver logs de un servicio
docker logs itm_festival_inventory_service
docker logs itm_festival_order_service
docker logs itm_festival_api_gateway

# Detener todos los contenedores
docker-compose down
```

## 🛠️ Solución de Problemas

### Si Swagger no se abre:
1. Verifica que `ASPNETCORE_ENVIRONMENT: Development` está configurado
2. Revisa los logs: `docker logs <nombre-contenedor>`
3. Asegúrate de que el puerto está disponible: `netstat -ano | findstr :5273`

### Si falla la conexión a SQL Server:
- La cadena de conexión usa `Server=sqlserver` (nombre del servicio en Docker)
- Espera 30+ segundos a que SQL Server se inicie completamente
- Verifica el healthcheck: `docker ps` y confirma el estado

### Si hay errores de tipo gRPC:
- Asegúrate de que los protobuf están generados: `dotnet build`
- Revisa que `Grpc.Tools` está en ambos `.csproj`

## 📝 Notas

- **Swashbuckle 6.4.0** es la versión más reciente compatible con .NET 10 y ASP.NET Core 10.0
- Los servicios se descubren entre sí usando nombres de DNS del contenedor (ej: `http://inventory-service:8080`)
- Los puertos HTTPS (8443) están disponibles pero no requieren certificados autofirmados en desarrollo
