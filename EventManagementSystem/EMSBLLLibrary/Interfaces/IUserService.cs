using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    public interface IUserService
    {
        Task<UserDto> GetById(int id);
        Task<PagedResult<UserDto>> GetAll(UserSearchRequest request);
        Task<UserDto> Update(int id, UpdateUserRequest request);
        Task ChangePassword(int id, ChangePasswordRequest request);
        Task ChangeEmail(int id, ChangeEmailRequest request);
        Task Deactivate(int id);
        Task DeactivateSelf(int id, string password);

        // Organizer role requests (user-facing)
        Task<OrganizerRequestDto> RequestOrganizerRole(int userId);
        Task<OrganizerRequestDto?> GetMyOrganizerRequest(int userId);

        // Admin operations
        Task<PagedResult<OrganizerRequestDto>> GetOrganizerRequests(OrganizerRequestQueryRequest request);
        Task<OrganizerRequestDto> ApproveOrganizerRequest(int requestId, int adminId);
        Task<OrganizerRequestDto> RejectOrganizerRequest(int requestId, int adminId, string? reason);
    }
}
