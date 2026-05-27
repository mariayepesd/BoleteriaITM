using Itm.Store.Mobile.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace BoleteriaITM
{
    public static class MauiProgram
    {
        // URL del gateway — se sobreescribe en runtime según la plataforma
        public static string GatewayUrl { get; private set; } = "http://localhost:5000";

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // La URL del emulador Android apunta a la máquina host con 10.0.2.2
#if ANDROID
            GatewayUrl = "http://10.0.2.2:5000";
#else
            GatewayUrl = "http://localhost:5000";
#endif

            // AuthHandler: inyecta el JWT de SecureStorage en cada petición
            builder.Services.AddTransient<AuthHandler>();

            // HttpClient hacia el Gateway con el handler de autenticación
            builder.Services.AddHttpClient("GatewayClient", client =>
            {
                client.BaseAddress = new Uri(GatewayUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<AuthHandler>();

            // SignalR HubConnection como singleton para que persista entre navegaciones
            builder.Services.AddSingleton(_ =>
                new HubConnectionBuilder()
                    .WithUrl($"{GatewayUrl}/hubs/tickets")
                    .WithAutomaticReconnect()
                    .Build());

            // Páginas registradas en DI para recibir inyección de dependencias
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<AppShell>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
