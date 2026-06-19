using System.ComponentModel.DataAnnotations;

namespace EMSModelLibrary.DTOs
{
    public class TicketTypeDto
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
    }

    public class CreateTicketTypeRequest
    {
        [Range(1, int.MaxValue)]
        public int EventId { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string SeatType { get; set; } = string.Empty;

        [Range(0.0, 100000.0)]
        public decimal Price { get; set; }

        [Range(1, 100000)]
        public int TotalQuantity { get; set; }

        [Required]
        public DateTime SaleStart { get; set; }

        [Required]
        public DateTime SaleEnd { get; set; }
    }

    public class UpdateTicketTypeRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string SeatType { get; set; } = string.Empty;

        [Range(0.0, 100000.0)]
        public decimal Price { get; set; }

        [Range(1, 100000)]
        public int TotalQuantity { get; set; }

        [Required]
        public DateTime SaleStart { get; set; }

        [Required]
        public DateTime SaleEnd { get; set; }

        public bool IsActive { get; set; }
    }
}
