using AutoMapper;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Mappings;
using EMSBLLLibrary.Services;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Exceptions;
using EMSModelLibrary.Models;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class UserServiceTests
    {
        private Mock<IUserRepository> _userRepo;
        private Mock<IOrganizerRequestRepository> _orgRequestRepo;
        private Mock<IRefreshTokenRepository> _refreshTokenRepo;
        private IMapper _mapper;
        private UserService _sut;

        [SetUp]
        public void SetUp()
        {
            _userRepo = new Mock<IUserRepository>();
            _orgRequestRepo = new Mock<IOrganizerRequestRepository>();
            _refreshTokenRepo = new Mock<IRefreshTokenRepository>();
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
            _sut = new UserService(_userRepo.Object, _orgRequestRepo.Object, _refreshTokenRepo.Object, _mapper);
        }

        private User MakeUser(int id = 1, string role = "User") => new User
        {
            Id = id, Name = "John", Email = "john@example.com", Phone = "9876543210",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@1234"),
            Role = role, IsActive = true
        };

        // ── GetById / GetAll ─────────────────────────────────────────────────────

        [Test]
        public async Task GetById_Found_ReturnsDto()
        {
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeUser());

            var result = await _sut.GetById(1);

            result.Email.Should().Be("john@example.com");
        }

        [Test]
        public async Task GetById_NotFound_ThrowsNotFoundException()
        {
            _userRepo.Setup(r => r.GetById(99)).ReturnsAsync((User?)null);

            await _sut.Invoking(s => s.GetById(99)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task GetAll_ReturnsMappedList()
        {
            var users = new List<User> { MakeUser(), MakeUser(id: 2) };
            _userRepo.Setup(r => r.Search(null, null, null, 1, 20)).ReturnsAsync((users, 2));

            var result = await _sut.GetAll(new UserSearchRequest { Page = 1, PageSize = 20 });

            result.Items.Should().HaveCount(2);
            result.TotalCount.Should().Be(2);
        }

        // ── Update ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Update_ValidRequest_ReturnsUpdatedDto()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.Update(It.IsAny<User>())).ReturnsAsync(user);

            var result = await _sut.Update(1, new UpdateUserRequest { Name = "Jane", Phone = "9999999999" });

            result.Name.Should().Be("Jane");
        }

        [Test]
        public async Task Update_NotFound_ThrowsNotFoundException()
        {
            _userRepo.Setup(r => r.GetById(99)).ReturnsAsync((User?)null);

            await _sut.Invoking(s => s.Update(99, new UpdateUserRequest { Name = "Jane", Phone = "9999999999" }))
                .Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Update_InvalidName_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.Update(1, new UpdateUserRequest { Name = "X", Phone = "9876543210" }))
                .Should().ThrowAsync<ValidationException>();
        }

        [Test]
        public async Task Update_InvalidPhone_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.Update(1, new UpdateUserRequest { Name = "John Doe", Phone = "abc" }))
                .Should().ThrowAsync<ValidationException>();
        }

        // ── ChangePassword ────────────────────────────────────────────────────────

        [Test]
        public async Task ChangePassword_Correct_UpdatesHash()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.Update(It.IsAny<User>())).ReturnsAsync(user);
            _refreshTokenRepo.Setup(r => r.RevokeByUserId(1)).Returns(Task.CompletedTask);

            await _sut.ChangePassword(1, new ChangePasswordRequest
            {
                CurrentPassword = "Pass@1234",
                NewPassword = "NewPass@9876"
            });

            _userRepo.Verify(r => r.Update(It.IsAny<User>()), Times.Once);
        }

        [Test]
        public async Task ChangePassword_WrongCurrent_ThrowsValidationException()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);

            await _sut.Invoking(s => s.ChangePassword(1, new ChangePasswordRequest
            {
                CurrentPassword = "Wrong@1234",
                NewPassword = "NewPass@9876"
            })).Should().ThrowAsync<ValidationException>().WithMessage("*incorrect*");
        }

        [Test]
        public async Task ChangePassword_NotFound_ThrowsNotFoundException()
        {
            _userRepo.Setup(r => r.GetById(99)).ReturnsAsync((User?)null);

            await _sut.Invoking(s => s.ChangePassword(99, new ChangePasswordRequest
            {
                CurrentPassword = "Pass@1234", NewPassword = "NewPass@9876"
            })).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task ChangePassword_WeakNewPassword_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.ChangePassword(1, new ChangePasswordRequest
            {
                CurrentPassword = "Pass@1234", NewPassword = "weak"
            })).Should().ThrowAsync<ValidationException>();
        }

        // ── ChangeEmail ───────────────────────────────────────────────────────────

        [Test]
        public async Task ChangeEmail_Valid_UpdatesEmail()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.EmailExists("new@example.com")).ReturnsAsync(false);
            _userRepo.Setup(r => r.Update(It.IsAny<User>())).ReturnsAsync(user);
            _refreshTokenRepo.Setup(r => r.RevokeByUserId(1)).Returns(Task.CompletedTask);

            await _sut.ChangeEmail(1, new ChangeEmailRequest { NewEmail = "new@example.com", Password = "Pass@1234" });

            _userRepo.Verify(r => r.Update(It.Is<User>(u => u.Email == "new@example.com")), Times.Once);
        }

        [Test]
        public async Task ChangeEmail_NotFound_ThrowsNotFoundException()
        {
            _userRepo.Setup(r => r.GetById(99)).ReturnsAsync((User?)null);

            await _sut.Invoking(s => s.ChangeEmail(99, new ChangeEmailRequest
            {
                NewEmail = "x@x.com", Password = "Pass@1234"
            })).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task ChangeEmail_WrongPassword_ThrowsValidationException()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);

            await _sut.Invoking(s => s.ChangeEmail(1, new ChangeEmailRequest
            {
                NewEmail = "new@example.com", Password = "Wrong@1234"
            })).Should().ThrowAsync<ValidationException>().WithMessage("*incorrect*");
        }

        [Test]
        public async Task ChangeEmail_EmailInUse_ThrowsValidationException()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.EmailExists("taken@example.com")).ReturnsAsync(true);

            await _sut.Invoking(s => s.ChangeEmail(1, new ChangeEmailRequest
            {
                NewEmail = "taken@example.com", Password = "Pass@1234"
            })).Should().ThrowAsync<ValidationException>().WithMessage("*already in use*");
        }

        [Test]
        public async Task ChangeEmail_InvalidEmailFormat_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.ChangeEmail(1, new ChangeEmailRequest
            {
                NewEmail = "not-valid", Password = "Pass@1234"
            })).Should().ThrowAsync<ValidationException>();
        }

        // ── Deactivate / DeactivateSelf ───────────────────────────────────────────

        [Test]
        public async Task Deactivate_ExistingUser_SetsIsActiveFalse()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.Update(It.IsAny<User>())).ReturnsAsync(user);
            _refreshTokenRepo.Setup(r => r.RevokeByUserId(1)).Returns(Task.CompletedTask);

            await _sut.Deactivate(1);

            _userRepo.Verify(r => r.Update(It.Is<User>(u => !u.IsActive)), Times.Once);
        }

        [Test]
        public async Task Deactivate_NotFound_ThrowsNotFoundException()
        {
            _userRepo.Setup(r => r.GetById(99)).ReturnsAsync((User?)null);

            await _sut.Invoking(s => s.Deactivate(99)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task DeactivateSelf_CorrectPassword_SetsIsActiveFalse()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.Update(It.IsAny<User>())).ReturnsAsync(user);
            _refreshTokenRepo.Setup(r => r.RevokeByUserId(1)).Returns(Task.CompletedTask);

            await _sut.DeactivateSelf(1, "Pass@1234");

            _userRepo.Verify(r => r.Update(It.Is<User>(u => !u.IsActive)), Times.Once);
        }

        [Test]
        public async Task DeactivateSelf_WrongPassword_ThrowsValidationException()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);

            await _sut.Invoking(s => s.DeactivateSelf(1, "Wrong@1234"))
                .Should().ThrowAsync<ValidationException>().WithMessage("*incorrect*");
        }

        [Test]
        public async Task DeactivateSelf_NotFound_ThrowsNotFoundException()
        {
            _userRepo.Setup(r => r.GetById(99)).ReturnsAsync((User?)null);

            await _sut.Invoking(s => s.DeactivateSelf(99, "Pass@1234")).Should().ThrowAsync<NotFoundException>();
        }

        // ── Organizer Requests ────────────────────────────────────────────────────

        [Test]
        public async Task RequestOrganizerRole_NewRequest_ReturnsDto()
        {
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeUser(role: "User"));
            _orgRequestRepo.Setup(r => r.GetPendingByUserId(1)).ReturnsAsync((OrganizerRequest?)null);
            _orgRequestRepo.Setup(r => r.Add(It.IsAny<OrganizerRequest>())).ReturnsAsync((OrganizerRequest o) => o);

            var result = await _sut.RequestOrganizerRole(1);

            result.Status.Should().Be("Pending");
        }

        [Test]
        public async Task RequestOrganizerRole_AlreadyOrganizer_ThrowsValidationException()
        {
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeUser(role: "Organizer"));

            await _sut.Invoking(s => s.RequestOrganizerRole(1))
                .Should().ThrowAsync<ValidationException>().WithMessage("*already an organizer*");
        }

        [Test]
        public async Task RequestOrganizerRole_AlreadyAdmin_ThrowsValidationException()
        {
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeUser(role: "Admin"));

            await _sut.Invoking(s => s.RequestOrganizerRole(1))
                .Should().ThrowAsync<ValidationException>();
        }

        [Test]
        public async Task RequestOrganizerRole_PendingRequestExists_ThrowsValidationException()
        {
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeUser());
            _orgRequestRepo.Setup(r => r.GetPendingByUserId(1)).ReturnsAsync(new OrganizerRequest { UserId = 1 });

            await _sut.Invoking(s => s.RequestOrganizerRole(1))
                .Should().ThrowAsync<ValidationException>().WithMessage("*pending*");
        }

        [Test]
        public async Task RequestOrganizerRole_UserNotFound_ThrowsNotFoundException()
        {
            _userRepo.Setup(r => r.GetById(99)).ReturnsAsync((User?)null);

            await _sut.Invoking(s => s.RequestOrganizerRole(99)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task GetMyOrganizerRequest_WithRequest_ReturnsDto()
        {
            var req = new OrganizerRequest { Id = 1, UserId = 1, Status = "Pending" };
            _orgRequestRepo.Setup(r => r.GetLatestByUserId(1)).ReturnsAsync(req);
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeUser());

            var result = await _sut.GetMyOrganizerRequest(1);

            result.Should().NotBeNull();
        }

        [Test]
        public async Task GetMyOrganizerRequest_NoRequest_ReturnsNull()
        {
            _orgRequestRepo.Setup(r => r.GetLatestByUserId(1)).ReturnsAsync((OrganizerRequest?)null);

            var result = await _sut.GetMyOrganizerRequest(1);

            result.Should().BeNull();
        }

        [Test]
        public async Task GetOrganizerRequests_NoFilter_ReturnsAll()
        {
            var req = new OrganizerRequest { Id = 1, UserId = 1 };
            _orgRequestRepo.Setup(r => r.SearchPaged(null, 1, 20)).ReturnsAsync((new List<OrganizerRequest> { req }, 1));
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeUser());

            var result = await _sut.GetOrganizerRequests(new OrganizerRequestQueryRequest { Page = 1, PageSize = 20 });

            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
        }

        [Test]
        public async Task GetOrganizerRequests_WithFilter_CallsGetByStatus()
        {
            var req = new OrganizerRequest { Id = 1, UserId = 1, Status = "Pending" };
            _orgRequestRepo.Setup(r => r.SearchPaged("Pending", 1, 20)).ReturnsAsync((new List<OrganizerRequest> { req }, 1));
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeUser());

            var result = await _sut.GetOrganizerRequests(new OrganizerRequestQueryRequest { Status = "Pending", Page = 1, PageSize = 20 });

            result.Items.Should().HaveCount(1);
        }

        [Test]
        public async Task ApproveOrganizerRequest_ValidPending_SetsRoleAndApproves()
        {
            var req = new OrganizerRequest { Id = 1, UserId = 1, Status = OrganizerRequestStatus.Pending };
            var user = MakeUser();
            _orgRequestRepo.Setup(r => r.GetById(1)).ReturnsAsync(req);
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);
            _orgRequestRepo.Setup(r => r.Update(It.IsAny<OrganizerRequest>())).ReturnsAsync(req);
            _userRepo.Setup(r => r.Update(It.IsAny<User>())).ReturnsAsync(user);

            var result = await _sut.ApproveOrganizerRequest(1, 99);

            result.Status.Should().Be(OrganizerRequestStatus.Approved);
            user.Role.Should().Be("Organizer");
        }

        [Test]
        public async Task ApproveOrganizerRequest_NotFound_ThrowsNotFoundException()
        {
            _orgRequestRepo.Setup(r => r.GetById(99)).ReturnsAsync((OrganizerRequest?)null);

            await _sut.Invoking(s => s.ApproveOrganizerRequest(99, 1)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task ApproveOrganizerRequest_NotPending_ThrowsValidationException()
        {
            _orgRequestRepo.Setup(r => r.GetById(1)).ReturnsAsync(new OrganizerRequest { Status = OrganizerRequestStatus.Approved });

            await _sut.Invoking(s => s.ApproveOrganizerRequest(1, 1))
                .Should().ThrowAsync<ValidationException>().WithMessage("*not pending*");
        }

        [Test]
        public async Task ApproveOrganizerRequest_UserNotFound_ThrowsNotFoundException()
        {
            var req = new OrganizerRequest { Id = 1, UserId = 99, Status = OrganizerRequestStatus.Pending };
            _orgRequestRepo.Setup(r => r.GetById(1)).ReturnsAsync(req);
            _userRepo.Setup(r => r.GetById(99)).ReturnsAsync((User?)null);

            await _sut.Invoking(s => s.ApproveOrganizerRequest(1, 1)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task RejectOrganizerRequest_ValidPending_SetsRejected()
        {
            var req = new OrganizerRequest { Id = 1, UserId = 1, Status = OrganizerRequestStatus.Pending };
            _orgRequestRepo.Setup(r => r.GetById(1)).ReturnsAsync(req);
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeUser());
            _orgRequestRepo.Setup(r => r.Update(It.IsAny<OrganizerRequest>())).ReturnsAsync(req);

            var result = await _sut.RejectOrganizerRequest(1, 99, "Not eligible");

            result.Status.Should().Be(OrganizerRequestStatus.Rejected);
        }

        [Test]
        public async Task RejectOrganizerRequest_NotFound_ThrowsNotFoundException()
        {
            _orgRequestRepo.Setup(r => r.GetById(99)).ReturnsAsync((OrganizerRequest?)null);

            await _sut.Invoking(s => s.RejectOrganizerRequest(99, 1, null)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task RejectOrganizerRequest_NotPending_ThrowsValidationException()
        {
            _orgRequestRepo.Setup(r => r.GetById(1)).ReturnsAsync(new OrganizerRequest { Status = OrganizerRequestStatus.Rejected });

            await _sut.Invoking(s => s.RejectOrganizerRequest(1, 1, null))
                .Should().ThrowAsync<ValidationException>().WithMessage("*not pending*");
        }
    }
}
