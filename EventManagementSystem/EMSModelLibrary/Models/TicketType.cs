namespace EMSModelLibrary.Models
{
    public class TicketType
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SeatType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TotalQuantity { get; set; }
        public int AvailableQuantity { get; set; }
        public DateTime SaleStart { get; set; }
        public DateTime SaleEnd { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public TicketType()
        {
            CreatedAt = DateTime.UtcNow;
        }

        public TicketType(int id, int eventId, string name, decimal price, int quantityAvailable, DateTime saleStart, DateTime saleEnd, bool isActive)
        {
            Id = id;
            EventId = eventId;
            Name = name;
            Price = price;
            TotalQuantity = quantityAvailable;
            AvailableQuantity = quantityAvailable;
            SaleStart = saleStart;
            SaleEnd = saleEnd;
            IsActive = isActive;
            CreatedAt = DateTime.UtcNow;
        }
    }
}