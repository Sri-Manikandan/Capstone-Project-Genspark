using AutoMapper;
using EMSBLLLibrary.Mappings;
using EMSBLLLibrary.Services;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Exceptions;
using EMSModelLibrary.Models;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class AuthServiceTests
    {
        private Mock<IUserRepository> _userRepo;
        private Mock<IRefreshTokenRepository> _refreshTokenRepo;
        private IConfiguration _config;
        private IMapper _mapper;
        private IMemoryCache _cache;
        private AuthService _sut;

        [SetUp]
        public void SetUp()
        {
            _userRepo = new Mock<IUserRepository>();
            _refreshTokenRepo = new Mock<IRefreshTokenRepository>();
            _cache = new MemoryCache(new MemoryCacheOptions());

            var configData = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "TestSecretKeyForJwtTokenGeneration2024!@#$",
                ["Jwt:Issuer"] = "EMSApi",
                ["Jwt:Audience"] = "EMSClient",
                ["Jwt:AccessTokenExpiryMinutes"] = "60",
                ["Jwt:RefreshTokenExpiryDays"] = "7"
            };
            _config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
            _sut = new AuthService(_userRepo.Object, _refreshTokenRepo.Object, _config, _mapper, _cache);
        }

        // ── Register ─────────────────────────────────────────────────────────────

        [Test]
        public async Task Register_ValidRequest_ReturnsAuthResponse()
        {
            _userRepo.Setup(r => r.EmailExists("john@example.com")).ReturnsAsync(false);
            _userRepo.Setup(r => r.Add(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _refreshTokenRepo.Setup(r => r.Add(It.IsAny<RefreshToken>())).ReturnsAsync(new RefreshToken());

            var result = await _sut.Register(new RegisterRequest
            {
                Name = "John Doe",
                Email = "john@example.com",
                Phone = "9876543210",
                Password = "Pass@1234"
            });

            result.AccessToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBeNullOrEmpty();
            result.User.Email.Should().Be("john@example.com");
        }

        [Test]
        public async Task Register_DuplicateEmail_ThrowsValidationException()
        {
            _userRepo.Setup(r => r.EmailExists("john@example.com")).ReturnsAsync(true);

            await _sut.Invoking(s => s.Register(new RegisterRequest
            {
                Name = "John Doe",
                Email = "john@example.com",
                Phone = "9876543210",
                Password = "Pass@1234"
            })).Should().ThrowAsync<ValidationException>().WithMessage("*already registered*");
        }

        [Test]
        public async Task Register_EmptyName_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.Register(new RegisterRequest
            {
                Name = "",
                Email = "john@example.com",
                Phone = "9876543210",
                Password = "Pass@1234"
            })).Should().ThrowAsync<ValidationException>().WithMessage("*Name*");
        }

        [Test]
        public async Task Register_InvalidEmail_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.Register(new RegisterRequest
            {
                Name = "John",
                Email = "not-an-email",
                Phone = "9876543210",
                Password = "Pass@1234"
            })).Should().ThrowAsync<ValidationException>();
        }

        [Test]
        public async Task Register_InvalidPhone_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.Register(new RegisterRequest
            {
                Name = "John",
                Email = "john@example.com",
                Phone = "abc",
                Password = "Pass@1234"
            })).Should().ThrowAsync<ValidationException>();
        }

        [Test]
        public async Task Register_WeakPassword_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.Register(new RegisterRequest
            {
                Name = "John",
                Email = "john@example.com",
                Phone = "9876543210",
                Password = "password"
            })).Should().ThrowAsync<ValidationException>();
        }

        // ── Login ────────────────────────────────────────────────────────────────

        [Test]
        public async Task Login_ValidCredentials_ReturnsAuthResponse()
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("Pass@1234");
            var user = new User { Id = 1, Email = "john@example.com", PasswordHash = hash, IsActive = true, Role = "User", Name = "John" };
            _userRepo.Setup(r => r.GetByEmail("john@example.com")).ReturnsAsync(user);
            _refreshTokenRepo.Setup(r => r.Add(It.IsAny<RefreshToken>())).ReturnsAsync(new RefreshToken());

            var result = await _sut.Login(new LoginRequest { Email = "john@example.com", Password = "Pass@1234" });

            result.AccessToken.Should().NotBeNullOrEmpty();
        }

        [Test]
        public async Task Login_UserNotFound_ThrowsInvalidCredentialsException()
        {
            _userRepo.Setup(r => r.GetByEmail("x@x.com")).ReturnsAsync((User?)null);

            await _sut.Invoking(s => s.Login(new LoginRequest { Email = "x@x.com", Password = "Pass@1234" }))
                .Should().ThrowAsync<InvalidCredentialsException>();
        }

        [Test]
        public async Task Login_InactiveUser_ThrowsInvalidCredentialsException()
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("Pass@1234");
            var user = new User { Email = "john@example.com", PasswordHash = hash, IsActive = false };
            _userRepo.Setup(r => r.GetByEmail("john@example.com")).ReturnsAsync(user);

            await _sut.Invoking(s => s.Login(new LoginRequest { Email = "john@example.com", Password = "Pass@1234" }))
                .Should().ThrowAsync<InvalidCredentialsException>().WithMessage("*deactivated*");
        }

        [Test]
        public async Task Login_WrongPassword_ThrowsInvalidCredentialsException()
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("Pass@1234");
            var user = new User { Email = "john@example.com", PasswordHash = hash, IsActive = true };
            _userRepo.Setup(r => r.GetByEmail("john@example.com")).ReturnsAsync(user);

            await _sut.Invoking(s => s.Login(new LoginRequest { Email = "john@example.com", Password = "Wrong@1234" }))
                .Should().ThrowAsync<InvalidCredentialsException>();
        }

        // ── RefreshToken ─────────────────────────────────────────────────────────

        [Test]
        public async Task RefreshToken_ValidToken_ReturnsNewAuthResponse()
        {
            var user = new User { Id = 1, Email = "j@j.com", IsActive = true, Role = "User", Name = "J", PasswordHash = "x" };
            var token = new RefreshToken { UserId = 1, Token = "tok", ExpiresAt = DateTime.UtcNow.AddDays(7), RevokedAt = null };
            _refreshTokenRepo.Setup(r => r.GetByToken("tok")).ReturnsAsync(token);
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);
            _refreshTokenRepo.Setup(r => r.Update(It.IsAny<RefreshToken>())).ReturnsAsync(token);
            _refreshTokenRepo.Setup(r => r.Add(It.IsAny<RefreshToken>())).ReturnsAsync(new RefreshToken());

            var result = await _sut.RefreshToken("tok");

            result.AccessToken.Should().NotBeNullOrEmpty();
        }

        [Test]
        public async Task RefreshToken_TokenNotFound_ThrowsInvalidCredentialsException()
        {
            _refreshTokenRepo.Setup(r => r.GetByToken("bad")).ReturnsAsync((RefreshToken?)null);

            await _sut.Invoking(s => s.RefreshToken("bad")).Should().ThrowAsync<InvalidCredentialsException>();
        }

        [Test]
        public async Task RefreshToken_RevokedToken_ThrowsInvalidCredentialsException()
        {
            var token = new RefreshToken { Token = "tok", ExpiresAt = DateTime.UtcNow.AddDays(7), RevokedAt = DateTime.UtcNow };
            _refreshTokenRepo.Setup(r => r.GetByToken("tok")).ReturnsAsync(token);

            await _sut.Invoking(s => s.RefreshToken("tok")).Should().ThrowAsync<InvalidCredentialsException>().WithMessage("*revoked*");
        }

        [Test]
        public async Task RefreshToken_ExpiredToken_ThrowsInvalidCredentialsException()
        {
            var token = new RefreshToken { Token = "tok", ExpiresAt = DateTime.UtcNow.AddDays(-1), RevokedAt = null };
            _refreshTokenRepo.Setup(r => r.GetByToken("tok")).ReturnsAsync(token);

            await _sut.Invoking(s => s.RefreshToken("tok")).Should().ThrowAsync<InvalidCredentialsException>().WithMessage("*expired*");
        }

        [Test]
        public async Task RefreshToken_UserNotFound_ThrowsInvalidCredentialsException()
        {
            var token = new RefreshToken { UserId = 99, Token = "tok", ExpiresAt = DateTime.UtcNow.AddDays(7), RevokedAt = null };
            _refreshTokenRepo.Setup(r => r.GetByToken("tok")).ReturnsAsync(token);
            _userRepo.Setup(r => r.GetById(99)).ReturnsAsync((User?)null);

            await _sut.Invoking(s => s.RefreshToken("tok")).Should().ThrowAsync<InvalidCredentialsException>();
        }

        [Test]
        public async Task RefreshToken_InactiveUser_ThrowsInvalidCredentialsException()
        {
            var user = new User { Id = 1, IsActive = false };
            var token = new RefreshToken { UserId = 1, Token = "tok", ExpiresAt = DateTime.UtcNow.AddDays(7), RevokedAt = null };
            _refreshTokenRepo.Setup(r => r.GetByToken("tok")).ReturnsAsync(token);
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);
            _refreshTokenRepo.Setup(r => r.Update(It.IsAny<RefreshToken>())).ReturnsAsync(token);

            await _sut.Invoking(s => s.RefreshToken("tok")).Should().ThrowAsync<InvalidCredentialsException>().WithMessage("*deactivated*");
        }

        // ── Logout ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Logout_ValidToken_RevokesToken()
        {
            var token = new RefreshToken { Token = "tok", RevokedAt = null };
            _refreshTokenRepo.Setup(r => r.GetByToken("tok")).ReturnsAsync(token);
            _refreshTokenRepo.Setup(r => r.Update(It.IsAny<RefreshToken>())).ReturnsAsync(token);

            await _sut.Logout("tok");

            _refreshTokenRepo.Verify(r => r.Update(It.Is<RefreshToken>(t => t.RevokedAt != null)), Times.Once);
        }

        [Test]
        public async Task Logout_AlreadyRevoked_DoesNotCallUpdate()
        {
            var token = new RefreshToken { Token = "tok", RevokedAt = DateTime.UtcNow };
            _refreshTokenRepo.Setup(r => r.GetByToken("tok")).ReturnsAsync(token);

            await _sut.Logout("tok");

            _refreshTokenRepo.Verify(r => r.Update(It.IsAny<RefreshToken>()), Times.Never);
        }

        [Test]
        public async Task Logout_TokenNotFound_DoesNotThrow()
        {
            _refreshTokenRepo.Setup(r => r.GetByToken("missing")).ReturnsAsync((RefreshToken?)null);

            await _sut.Invoking(s => s.Logout("missing")).Should().NotThrowAsync();
        }

        // ── ForgotPassword ───────────────────────────────────────────────────────

        [Test]
        public async Task ForgotPassword_ExistingActiveUser_ReturnsToken()
        {
            var user = new User { Id = 1, Email = "j@j.com", IsActive = true };
            _userRepo.Setup(r => r.GetByEmail("j@j.com")).ReturnsAsync(user);

            var result = await _sut.ForgotPassword("j@j.com");

            result.ResetToken.Should().NotBeNullOrEmpty();
        }

        [Test]
        public async Task ForgotPassword_UnknownEmail_ReturnsSilentSuccess()
        {
            _userRepo.Setup(r => r.GetByEmail("x@x.com")).ReturnsAsync((User?)null);

            var result = await _sut.ForgotPassword("x@x.com");

            result.ResetToken.Should().BeEmpty();
        }

        [Test]
        public async Task ForgotPassword_InactiveUser_ReturnsSilentSuccess()
        {
            var user = new User { Id = 1, Email = "j@j.com", IsActive = false };
            _userRepo.Setup(r => r.GetByEmail("j@j.com")).ReturnsAsync(user);

            var result = await _sut.ForgotPassword("j@j.com");

            result.ResetToken.Should().BeEmpty();
        }

        // ── ResetPassword ────────────────────────────────────────────────────────

        [Test]
        public async Task ResetPassword_ValidToken_UpdatesPassword()
        {
            var user = new User { Id = 1, PasswordHash = "old" };
            _userRepo.Setup(r => r.GetByEmail("j@j.com")).ReturnsAsync(new User { Id = 1, IsActive = true });
            var fp = await _sut.ForgotPassword("j@j.com"); // stores token in cache
            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.Update(It.IsAny<User>())).ReturnsAsync(user);
            _refreshTokenRepo.Setup(r => r.RevokeByUserId(1)).Returns(Task.CompletedTask);

            await _sut.ResetPassword(new ResetPasswordRequest { Token = fp.ResetToken, NewPassword = "NewPass@1234" });

            _userRepo.Verify(r => r.Update(It.Is<User>(u => u.PasswordHash != "old")), Times.Once);
        }

        [Test]
        public async Task ResetPassword_InvalidToken_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.ResetPassword(new ResetPasswordRequest { Token = "bad", NewPassword = "NewPass@1234" }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*Invalid or expired*");
        }

        [Test]
        public async Task ResetPassword_WeakPassword_ThrowsValidationException()
        {
            var user = new User { Id = 1, IsActive = true };
            _userRepo.Setup(r => r.GetByEmail("j@j.com")).ReturnsAsync(user);
            var fp = await _sut.ForgotPassword("j@j.com");

            await _sut.Invoking(s => s.ResetPassword(new ResetPasswordRequest { Token = fp.ResetToken, NewPassword = "weak" }))
                .Should().ThrowAsync<ValidationException>();
        }
    }
}
