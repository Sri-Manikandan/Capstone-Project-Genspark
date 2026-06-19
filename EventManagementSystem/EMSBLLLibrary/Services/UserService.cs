using AutoMapper;
using EMSBLLLibrary.Constants;
using EMSModelLibrary.DTOs;
using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;

namespace EMSBLLLibrary.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IOrganizerRequestRepository _orgRequestRepo;
        private readonly IRefreshTokenRepository _refreshTokenRepo;
        private readonly IMapper _mapper;

        public UserService(IUserRepository userRepo, IOrganizerRequestRepository orgRequestRepo, IRefreshTokenRepository refreshTokenRepo, IMapper mapper)
        {
            _userRepo = userRepo;
            _orgRequestRepo = orgRequestRepo;
            _refreshTokenRepo = refreshTokenRepo;
            _mapper = mapper;
        }

        public async Task<UserDto> GetById(int id)
        {
            var user = await _userRepo.GetById(id)
                ?? throw new NotFoundException($"User {id} not found.");
            return _mapper.Map<UserDto>(user);
        }

        public async Task<PagedResult<UserDto>> GetAll(UserSearchRequest request)
        {
            var (users, total) = await _userRepo.Search(request.Query, request.Role, request.IsActive, request.Page, request.PageSize);
            return new PagedResult<UserDto>
            {
                Items = _mapper.Map<List<UserDto>>(users),
                TotalCount = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        public async Task<UserDto> Update(int id, UpdateUserRequest request)
        {
            InputValidator.ValidateName(request.Name);
            InputValidator.ValidatePhone(request.Phone);

            var user = await _userRepo.GetById(id)
                ?? throw new NotFoundException($"User {id} not found.");

            user.Name = request.Name;
            user.Phone = request.Phone;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepo.Update(user);
            return _mapper.Map<UserDto>(user);
        }

        public async Task ChangePassword(int id, ChangePasswordRequest request)
        {
            InputValidator.ValidatePassword(request.NewPassword);

            var user = await _userRepo.GetById(id)
                ?? throw new NotFoundException($"User {id} not found.");

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                throw new ValidationException("Current password is incorrect.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.Update(user);
            await _refreshTokenRepo.RevokeByUserId(id);
        }

        public async Task ChangeEmail(int id, ChangeEmailRequest request)
        {
            InputValidator.ValidateEmail(request.NewEmail);

            var user = await _userRepo.GetById(id)
                ?? throw new NotFoundException($"User {id} not found.");

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new ValidationException("Password is incorrect.");

            if (await _userRepo.EmailExists(request.NewEmail))
                throw new ValidationException("That email address is already in use.");

            user.Email = request.NewEmail;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.Update(user);
            await _refreshTokenRepo.RevokeByUserId(id);
        }

        public async Task Deactivate(int id)
        {
            var user = await _userRepo.GetById(id)
                ?? throw new NotFoundException($"User {id} not found.");
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.Update(user);
            await _refreshTokenRepo.RevokeByUserId(id);
        }

        public async Task DeactivateSelf(int id, string password)
        {
            var user = await _userRepo.GetById(id)
                ?? throw new NotFoundException($"User {id} not found.");

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new ValidationException("Password is incorrect.");

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.Update(user);
            await _refreshTokenRepo.RevokeByUserId(id);
        }

        public async Task<OrganizerRequestDto> RequestOrganizerRole(int userId)
        {
            var user = await _userRepo.GetById(userId)
                ?? throw new NotFoundException($"User {userId} not found.");

            if (user.Role is "Organizer" or "Admin")
                throw new ValidationException("You are already an organizer or admin.");

            var pending = await _orgRequestRepo.GetPendingByUserId(userId);
            if (pending != null)
                throw new ValidationException("You already have a pending organizer request.");

            var request = new OrganizerRequest { UserId = userId };
            await _orgRequestRepo.Add(request);
            return BuildDto(request, user);
        }

        public async Task<OrganizerRequestDto?> GetMyOrganizerRequest(int userId)
        {
            var request = await _orgRequestRepo.GetLatestByUserId(userId);
            if (request == null) return null;

            var user = await _userRepo.GetById(userId);
            return BuildDto(request, user);
        }

        public async Task<PagedResult<OrganizerRequestDto>> GetOrganizerRequests(OrganizerRequestQueryRequest request)
        {
            var (requests, total) = await _orgRequestRepo.SearchPaged(request.Status, request.Page, request.PageSize);
            var items = new List<OrganizerRequestDto>();
            foreach (var req in requests)
            {
                var user = await _userRepo.GetById(req.UserId);
                items.Add(BuildDto(req, user));
            }
            return new PagedResult<OrganizerRequestDto>
            {
                Items = items,
                TotalCount = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        public async Task<OrganizerRequestDto> ApproveOrganizerRequest(int requestId, int adminId)
        {
            var request = await _orgRequestRepo.GetById(requestId)
                ?? throw new NotFoundException($"Organizer request {requestId} not found.");

            if (request.Status != OrganizerRequestStatus.Pending)
                throw new ValidationException($"Request is not pending. Current status: {request.Status}.");

            var user = await _userRepo.GetById(request.UserId)
                ?? throw new NotFoundException($"User {request.UserId} not found.");

            request.Status = OrganizerRequestStatus.Approved;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByAdminId = adminId;
            await _orgRequestRepo.Update(request);

            user.Role = "Organizer";
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.Update(user);

            return BuildDto(request, user);
        }

        public async Task<OrganizerRequestDto> RejectOrganizerRequest(int requestId, int adminId, string? reason)
        {
            var request = await _orgRequestRepo.GetById(requestId)
                ?? throw new NotFoundException($"Organizer request {requestId} not found.");

            if (request.Status != OrganizerRequestStatus.Pending)
                throw new ValidationException($"Request is not pending. Current status: {request.Status}.");

            var user = await _userRepo.GetById(request.UserId);

            request.Status = OrganizerRequestStatus.Rejected;
            request.Reason = reason;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByAdminId = adminId;
            await _orgRequestRepo.Update(request);

            return BuildDto(request, user);
        }

        private static OrganizerRequestDto BuildDto(OrganizerRequest request, User? user) => new()
        {
            Id = request.Id,
            UserId = request.UserId,
            UserName = user?.Name ?? string.Empty,
            UserEmail = user?.Email ?? string.Empty,
            Status = request.Status,
            Reason = request.Reason,
            RequestedAt = request.RequestedAt,
            ReviewedAt = request.ReviewedAt,
            ReviewedByAdminId = request.ReviewedByAdminId
        };
    }
}
