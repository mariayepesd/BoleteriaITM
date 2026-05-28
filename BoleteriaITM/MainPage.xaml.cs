using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace BoleteriaITM
{
    public partial class MainPage : ContentPage
    {
        private readonly HubConnection _hub;
        private readonly IHttpClientFactory _httpClientFactory;

        // La API devuelve JSON en camelCase; esto hace que la deserialización sea case-insensitive
        private static readonly System.Text.Json.JsonSerializerOptions _jsonOpts =
            new() { PropertyNameCaseInsensitive = true };

        // El OrderService hardcodea "usuario-demo" como UsuarioId
        // La app se suscribe a ese grupo para recibir la notificación de la boleta
        private const string CustomerId = "usuario-demo";

        private int _cantidad = 1;
        private int _productId = -1;

        // Mapa de (sedeIndex, categoriaIndex) → BoleteriaItemId del seed data
        // Medellín: 1=General, 2=VIP, 3=Palco | Madrid: 4=General, 5=VIP, 6=Palco
        private static int CalcProductId(int sede, int cat) => (sede * 3) + cat + 1;

        public MainPage(HubConnection hubConnection, IHttpClientFactory httpClientFactory)
        {
            InitializeComponent();
            _hub = hubConnection;
            _httpClientFactory = httpClientFactory;

            // Registrar handlers aquí para que no se dupliquen si OnAppearing se llama varias veces
            _hub.On<JsonElement>("ReceiveTicketReady", payload =>
                MainThread.BeginInvokeOnMainThread(() => MostrarBoleta(payload)));

            _hub.Reconnecting += _ =>
            {
                MainThread.BeginInvokeOnMainThread(() => ActualizarStatusSignalR("⟳ Reconectando...", "#f59e0b"));
                return Task.CompletedTask;
            };
            _hub.Reconnected += _ =>
            {
                MainThread.BeginInvokeOnMainThread(() => ActualizarStatusSignalR("● Conectado en tiempo real", "#4ade80"));
                return Task.CompletedTask;
            };
            _hub.Closed += _ =>
            {
                MainThread.BeginInvokeOnMainThread(() => ActualizarStatusSignalR("● Sin conexión en tiempo real", "#f87171"));
                return Task.CompletedTask;
            };
        }

        // ─── Ciclo de vida ────────────────────────────────────────────────────

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await ObtenerTokenAsync();
            await ConectarSignalRAsync();
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            if (_hub.State == HubConnectionState.Connected)
                await _hub.StopAsync();
        }

        // ─── Autenticación ────────────────────────────────────────────────────

        private async Task ObtenerTokenAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("GatewayClient");
                // El gateway expone /auth/token que genera un JWT de demo
                var resp = await client.PostAsync("/auth/token", null);
                if (resp.IsSuccessStatusCode)
                {
                    var result = await resp.Content.ReadFromJsonAsync<TokenResponse>(_jsonOpts);
                    if (result?.Token != null)
                        await SecureStorage.Default.SetAsync("jwt_token", result.Token);
                }
            }
            catch { /* la app funciona sin token; el gateway dejará pasar /api/orders */ }
        }

        // ─── SignalR ──────────────────────────────────────────────────────────

        private async Task ConectarSignalRAsync()
        {
            // Si ya está conectado (p.ej. segunda vez que OnAppearing se ejecuta), solo unirse al grupo
            if (_hub.State == HubConnectionState.Connected)
            {
                await _hub.InvokeAsync("JoinGroup", CustomerId);
                return;
            }

            if (_hub.State != HubConnectionState.Disconnected)
                return;

            try
            {
                await _hub.StartAsync();
                await _hub.InvokeAsync("JoinGroup", CustomerId);
                ActualizarStatusSignalR("● Conectado en tiempo real", "#4ade80");
            }
            catch (Exception ex)
            {
                ActualizarStatusSignalR($"● Sin conexión ({ex.Message})", "#f87171");
            }
        }

        private void ActualizarStatusSignalR(string texto, string colorHex)
        {
            LblSignalRStatus.Text = texto;
            LblSignalRStatus.TextColor = Color.FromArgb(colorHex);
        }

        // ─── Selección de producto ────────────────────────────────────────────

        private void OnSeleccionCambia(object? sender, EventArgs e)
        {
            var sede = PickerSede.SelectedIndex;
            var cat  = PickerCategoria.SelectedIndex;

            if (sede < 0 || cat < 0)
            {
                _productId = -1;
                PanelPrecio.IsVisible = false;
                BtnComprar.IsEnabled = false;
                return;
            }

            _productId = CalcProductId(sede, cat);
            _ = CargarPrecioAsync();
        }

        private async Task CargarPrecioAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("GatewayClient");
                var precio = await client.GetFromJsonAsync<PrecioResponse>($"/api/prices/{_productId}", _jsonOpts);
                if (precio == null) return;

                var total = precio.PrecioFinal * _cantidad;
                var simbolo = precio.Moneda == "COP" ? "$" : "€";
                LblPrecioTotal.Text = $"{simbolo}{total:N0} {precio.Moneda}";
                LblPrecioDetalle.Text = $"{simbolo}{precio.PrecioFinal:N0} × {_cantidad} boleta(s) · Demanda: {precio.NivelDemanda}";
                LblCacheTag.IsVisible = precio.FromCache;
                PanelPrecio.IsVisible = true;
                BtnComprar.IsEnabled = true;
            }
            catch
            {
                LblPrecioTotal.Text = "Precio no disponible";
                LblPrecioDetalle.Text = "Verifica la conexión con el gateway";
                PanelPrecio.IsVisible = true;
                BtnComprar.IsEnabled = false;
            }
        }

        // ─── Controles de cantidad ────────────────────────────────────────────

        private void OnMenos(object? sender, EventArgs e)
        {
            if (_cantidad <= 1) return;
            _cantidad--;
            LblCantidad.Text = _cantidad.ToString();
            if (_productId > 0) _ = CargarPrecioAsync();
        }

        private void OnMas(object? sender, EventArgs e)
        {
            if (_cantidad >= 10) return;
            _cantidad++;
            LblCantidad.Text = _cantidad.ToString();
            if (_productId > 0) _ = CargarPrecioAsync();
        }

        // ─── Compra (SAGA) ────────────────────────────────────────────────────

        private async void OnComprar(object? sender, EventArgs e)
        {
            if (_productId < 0) return;

            var correlationId = $"MAUI-{Guid.NewGuid():N}"[..16];

            // Preparar UI de carga
            BtnComprar.IsEnabled = false;
            PanelEstado.IsVisible = true;
            PanelTicketListo.IsVisible = false;
            Spinner.IsRunning = true;
            Spinner.IsVisible = true;
            LblEstado.Text = "⏳ Enviando al servidor...";
            LblCorrelationId.Text = $"Correlation ID: {correlationId}";
            LblCorrelationId.IsVisible = true;

            try
            {
                var client = _httpClientFactory.CreateClient("GatewayClient");

                // El Correlation ID viaja por todos los microservicios (OrderService → InventoryService)
                client.DefaultRequestHeaders.Remove("X-Correlation-ID");
                client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

                LblEstado.Text = "⏳ Verificando stock en InventoryService...";
                var resp = await client.PostAsJsonAsync("/api/orders",
                    new { ProductId = _productId, Quantity = _cantidad });

                if (resp.IsSuccessStatusCode)
                {
                    var orden = await resp.Content.ReadFromJsonAsync<OrdenResponse>(_jsonOpts);
                    LblEstado.Text = $"✅ Orden #{orden?.OrdenId} confirmada · Esperando boleta vía SignalR...";
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    LblEstado.Text = $"❌ {body}";
                    BtnComprar.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LblEstado.Text = $"❌ Error de red: {ex.Message}";
                BtnComprar.IsEnabled = true;
            }
            finally
            {
                Spinner.IsRunning = false;
                Spinner.IsVisible = false;
            }
        }

        // ─── Notificación en tiempo real (SignalR) ────────────────────────────

        private void MostrarBoleta(JsonElement payload)
        {
            // Este método se ejecuta en el hilo de UI gracias a MainThread.BeginInvokeOnMainThread
            PanelTicketListo.IsVisible = true;
            BtnComprar.IsEnabled = true;

            var orderId   = payload.TryGetProperty("orderId",   out var o) ? o.ToString()       : "—";
            var ticketUrl = payload.TryGetProperty("ticketUrl", out var u) ? u.GetString() ?? "" : "—";
            var message   = payload.TryGetProperty("message",   out var m) ? m.GetString() ?? "" : "";

            LblEstado.Text   = message.Length > 0 ? $"🎟 {message}" : "🎟 Boleta generada con éxito";
            LblOrdenInfo.Text  = $"Orden ID: {orderId}";
            LblTicketUrl.Text  = ticketUrl;
        }
    }

    // ─── DTOs ─────────────────────────────────────────────────────────────────

    record TokenResponse(string Token, int ExpiresIn);

    record PrecioResponse(
        int BoleteriaItemId, string NombreEvento, string Sede, string Categoria,
        decimal PrecioBase, decimal PrecioFinal, decimal Multiplicador,
        string Moneda, string NivelDemanda, bool FromCache);

    record OrdenResponse(int OrdenId, string Message, string Estado, decimal Total, string CorrelationId);
}
