using System.ComponentModel.DataAnnotations;

namespace EMSModelLibrary.DTOs
{
    public class SeatDto
    {
        public int Id { get; set; }
        public int VenueId { get; set; }
        public string Section { get; set; } = string.Empty;
        public string Row { get; set; } = string.Empty;
        public int SeatNumber { get; set; }
        public string SeatType { get; set; } = string.Empty;
    }

    public class CreateSeatRequest
    {
        [Range(1, int.MaxValue)]
        public int VenueId { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string Section { get; set; } = string.Empty;

        [Required]
        [StringLength(10, MinimumLength = 1)]
        public string Row { get; set; } = string.Empty;

        [Range(1, 10000)]
        public int SeatNumber { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string SeatType { get; set; } = string.Empty;
    }

    public class BulkCreateSeatsRequest
    {
        [Range(1, int.MaxValue)]
        public int VenueId { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string Section { get; set; } = string.Empty;

        [Required]
        [StringLength(10, MinimumLength = 1)]
        public string Row { get; set; } = string.Empty;

        [Range(1, 10000)]
        public int StartNumber { get; set; }

        [Range(1, 10000)]
        public int EndNumber { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 2)]
        public string SeatType { get; set; } = string.Empty;
    }
}
