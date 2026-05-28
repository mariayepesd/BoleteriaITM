
namespace Festival.Contracts
{

    public record OrderConfirmedEvent
    {
        public Guid OrderId { get; init; }
        public string CustomerId { get; init; } = string.Empty;
        public string EventName { get; init; } = string.Empty;
        public int TicketQuantity { get; init; }
        public decimal TotalAmount { get; init; }
    }
}
