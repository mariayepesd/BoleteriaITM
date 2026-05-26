namespace Itm.Price.Api.Data.Entities;

public class EventoPrecio
{
    public int Id { get; set; }

    // Referencia lógica al BoleteriaItem de InventoryService (sin FK real entre BDs)
    public int BoleteriaItemId { get; set; }

    public string NombreEvento { get; set; } = string.Empty;
    public string Sede { get; set; } = string.Empty;        // "Medellin" | "Madrid"
    public string Categoria { get; set; } = string.Empty;   // "General" | "VIP" | "Palco"
    public decimal PrecioBase { get; set; }
    public string Moneda { get; set; } = "COP";

    // Para calcular el porcentaje de ocupación y aplicar precios dinámicos
    public int StockTotal { get; set; }
    public int StockVendido { get; set; }
}
