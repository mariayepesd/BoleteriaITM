# ==========================================
# ETAPA 1: Compilación (Entorno del SDK)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /src

# 1. Copiar el archivo de proyecto y restaurar paquetes NuGet
# (Asegúrate de que el nombre del .csproj coincida)
COPY ["InventoryService.csproj", "./"]
RUN dotnet restore "InventoryService.csproj"

# 2. Copiar todo el código fuente y compilar la app internamente
COPY . .
RUN dotnet publish "InventoryService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# ETAPA 2: Ejecución (Entorno de Runtime ligero)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Mantener los puertos que ya tenías configurados
EXPOSE 8080
EXPOSE 8443

# Copiar los archivos compilados desde la Etapa 1
COPY --from=build-env /app/publish .

ENTRYPOINT ["dotnet", "InventoryService.dll"]