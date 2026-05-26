using Grpc.Core;
using Itm.Inventory.Api.Data;
using Itm.Inventory.Api.Protos;
using Microsoft.EntityFrameworkCore;

namespace Itm.Inventory.Api.Services;

public class GrpcInventoryService : InventoryService.InventoryServiceBase
{
    private readonly FestivalInventoryDbContext _db;

    public GrpcInventoryService(FestivalInventoryDbContext db) => _db = db;

    public override async Task<StockResponse> CheckStock(StockRequest request, ServerCallContext context)
    {
        var item = await _db.BoleteriaItems.FindAsync(request.ProductId);

        return new StockResponse
        {
            ProductId    = request.ProductId,
            Stock        = item?.StockDisponible ?? 0,
            IsAvailable  = item?.StockDisponible > 0
        };
    }
}
