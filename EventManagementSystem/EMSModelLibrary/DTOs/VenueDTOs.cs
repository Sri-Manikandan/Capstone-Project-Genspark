using System.ComponentModel.DataAnnotations;

namespace EMSModelLibrary.DTOs
{
    public class VenueDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public int TotalCapacity { get; set; }
        public string LayoutConfig { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateVenueRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(500, MinimumLength = 5)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string City { get; set; } = string.Empty;

        [Range(1, 100000)]
        public int TotalCapacity { get; set; }

        [Required]
        public string LayoutConfig { get; set; } = string.Empty;
    }

    public class UpdateVenueRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(500, MinimumLength = 5)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string City { get; set; } = string.Empty;

        [Range(1, 100000)]
        public int TotalCapacity { get; set; }

        [Required]
        public string LayoutConfig { get; set; } = string.Empty;
    }
}
