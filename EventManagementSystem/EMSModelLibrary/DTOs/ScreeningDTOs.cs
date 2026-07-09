using System.ComponentModel.DataAnnotations;

namespace EMSModelLibrary.DTOs
{
    public class ScreeningDto
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string Screen { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsSoldOut { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateScreeningRequest
    {
        [Range(1, int.MaxValue)]
        public int EventId { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string Screen { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }
    }

    public class UpdateScreeningRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string Screen { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }
    }
}
