namespace Itm.Order.Api.Data.Entities;

public class Orden
{
    public int Id { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public int BoleteriaItemId { get; set; }   // FK al BoleteriaItem en InventoryService
    public int Cantidad { get; set; }
    public decimal MontoTotal { get; set; }
    public OrdenEstado Estado { get; set; } = OrdenEstado.Pendiente;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
}

public enum OrdenEstado
{
    Pendiente,
    StockReservado,
    Pagada,
    Confirmada,
    Cancelada,
    Compensada
}
