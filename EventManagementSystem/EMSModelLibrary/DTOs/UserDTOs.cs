using System.ComponentModel.DataAnnotations;

namespace EMSModelLibrary.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateUserRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\+?[0-9]{7,15}$")]
        public string Phone { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ChangeEmailRequest
    {
        [Required]
        [EmailAddress]
        public string NewEmail { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class CloseAccountRequest
    {
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class UserSearchRequest
    {
        /// <summary>Partial match on name or email</summary>
        public string? Query { get; set; }

        /// <summary>Filter by role: User | Organizer | Admin</summary>
        public string? Role { get; set; }

        public bool? IsActive { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }
}
