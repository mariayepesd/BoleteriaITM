namespace Itm.Price.Api.Dtos;

public record PriceDto(
    int BoleteriaItemId,
    string NombreEvento,
    string Sede,
    string Categoria,
    decimal PrecioBase,
    decimal PrecioFinal,
    decimal Multiplicador,
    string Moneda,
    string NivelDemanda,   // "Normal" | "Alta" | "Muy Alta"
    bool FromCache
);
