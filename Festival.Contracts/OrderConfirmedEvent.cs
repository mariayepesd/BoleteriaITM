
namespace Festival.Contracts
{

    public record OrderConfirmedEvent
    {
        public Guid OrderId { get; init; }
        public string CustomerId { get; init; }
        public string EventName { get; init; }
        public int TicketQuantity { get; init; }
        public decimal TotalAmount { get; init; }
    }
}
