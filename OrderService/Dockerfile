FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /src

COPY ["Festival.Contracts/Festival.Contracts.csproj", "Festival.Contracts/"]
COPY ["OrderService/OrderService.csproj", "OrderService/"]
RUN dotnet restore "OrderService/OrderService.csproj"

COPY . .
WORKDIR "/src/OrderService"
RUN dotnet publish "OrderService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8080
COPY --from=build-env /app/publish .
ENTRYPOINT ["dotnet", "OrderService.dll"]
