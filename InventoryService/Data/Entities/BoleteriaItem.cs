namespace Itm.Inventory.Api.Data.Entities;

public class BoleteriaItem
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public Evento Evento { get; set; } = null!;

    // "General" | "VIP" | "Palco"
    public string Categoria { get; set; } = string.Empty;

    public int StockTotal { get; set; }
    public int StockDisponible { get; set; }
}
