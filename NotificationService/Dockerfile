FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /src

# Copiar el contrato compartido primero para que compile bien
COPY ["Festival.Contracts/Festival.Contracts.csproj", "Festival.Contracts/"]
COPY ["NotificationService/NotificationService.csproj", "NotificationService/"]
RUN dotnet restore "NotificationService/NotificationService.csproj"

COPY . .
WORKDIR "/src/NotificationService"
RUN dotnet publish "NotificationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8080
EXPOSE 8443
COPY --from=build-env /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.dll"]