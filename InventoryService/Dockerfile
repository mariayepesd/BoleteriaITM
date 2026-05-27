FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /src

COPY ["InventoryService.csproj", "./"]
RUN dotnet restore "InventoryService.csproj"

COPY . .
RUN dotnet publish "InventoryService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8080
COPY --from=build-env /app/publish .
ENTRYPOINT ["dotnet", "InventoryService.dll"]
