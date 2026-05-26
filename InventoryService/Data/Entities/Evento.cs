namespace Itm.Inventory.Api.Data.Entities;

public class Evento
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Sede { get; set; } = string.Empty; // "Medellin" | "Madrid"
    public bool EsActivo { get; set; } = true;

    public ICollection<BoleteriaItem> Items { get; set; } = new List<BoleteriaItem>();
}
