using AutoMapper;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Interfaces;
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
        private Mock<IEmailQueue> _emailQueue;
        private AuthService _sut;

        [SetUp]
        public void SetUp()
        {
            _userRepo = new Mock<IUserRepository>();
            _refreshTokenRepo = new Mock<IRefreshTokenRepository>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _emailQueue = new Mock<IEmailQueue>();

            var configData = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "TestSecretKeyForJwtTokenGeneration2024!@#$",
                ["Jwt:Issuer"] = "EMSApi",
                ["Jwt:Audience"] = "EMSClient",
                ["Jwt:AccessTokenExpiryMinutes"] = "60",
                ["Jwt:RefreshTokenExpiryDays"] = "7",
                ["Email:AppBaseUrl"] = "http://localhost:4200"
            };
            _config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
            _sut = new AuthService(_userRepo.Object, _refreshTokenRepo.Object, _config, _mapper, _cache, _emailQueue.Object);
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

        // The whole point of this feature: the reset token must never cross the wire in
        // the HTTP response. Anyone who knew an address could otherwise take the account.
        [Test]
        public async Task ForgotPassword_ShouldNotReturnTokenInResponse()
        {
            var user = new User { Id = 1, Name = "Jay", Email = "j@j.com", IsActive = true };
            _userRepo.Setup(r => r.GetByEmail("j@j.com")).ReturnsAsync(user);

            var result = await _sut.ForgotPassword("j@j.com");

            typeof(ForgotPasswordResponse).GetProperty("ResetToken").Should().BeNull(
                "returning a reset token in the HTTP response is account takeover");
            result.Message.Should().NotBeNullOrWhiteSpace();
        }

        [Test]
        public async Task ForgotPassword_ShouldEnqueueResetEmail_WhenUserExists()
        {
            var user = new User { Id = 1, Name = "Jay", Email = "j@j.com", IsActive = true };
            _userRepo.Setup(r => r.GetByEmail("j@j.com")).ReturnsAsync(user);

            IDictionary<string, string>? tokens = null;
            _emailQueue.Setup(q => q.Enqueue("j@j.com", "Jay", EmailTemplateKey.PasswordReset,
                    It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<DateTime?>()))
                .Callback<string, string, string, string, IDictionary<string, string>, string?, DateTime?>(
                    (_, _, _, _, t, _, _) => tokens = t)
                .Returns(Task.CompletedTask);

            await _sut.ForgotPassword("j@j.com");

            tokens.Should().NotBeNull();
            tokens!["ResetUrl"].Should().StartWith("http://localhost:4200/auth/reset-password?token=");
        }

        // Enqueueing for an unknown address would turn this endpoint into an email
        // enumeration oracle.
        [Test]
        public async Task ForgotPassword_UnknownEmail_ReturnsSilentSuccessAndSendsNothing()
        {
            _userRepo.Setup(r => r.GetByEmail("x@x.com")).ReturnsAsync((User?)null);

            var result = await _sut.ForgotPassword("x@x.com");

            result.Message.Should().NotBeNullOrWhiteSpace();
            _emailQueue.Verify(q => q.Enqueue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<DateTime?>()),
                Times.Never);
        }

        [Test]
        public async Task ForgotPassword_InactiveUser_ReturnsSilentSuccessAndSendsNothing()
        {
            var user = new User { Id = 1, Email = "j@j.com", IsActive = false };
            _userRepo.Setup(r => r.GetByEmail("j@j.com")).ReturnsAsync(user);

            var result = await _sut.ForgotPassword("j@j.com");

            result.Message.Should().NotBeNullOrWhiteSpace();
            _emailQueue.Verify(q => q.Enqueue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<DateTime?>()),
                Times.Never);
        }

        // ── ResetPassword ────────────────────────────────────────────────────────

        [Test]
        public async Task ResetPassword_ValidToken_UpdatesPassword()
        {
            var user = new User { Id = 1, PasswordHash = "old" };
            _userRepo.Setup(r => r.GetByEmail("j@j.com")).ReturnsAsync(new User { Id = 1, Name = "Jay", Email = "j@j.com", IsActive = true });

            // The token now only reaches the user through the emailed link, so the test
            // reads it back out of the queued email — exactly as a real user reads it
            // out of their inbox.
            IDictionary<string, string>? captured = null;
            _emailQueue.Setup(q => q.Enqueue(It.IsAny<string>(), It.IsAny<string>(), EmailTemplateKey.PasswordReset,
                    It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<DateTime?>()))
                .Callback<string, string, string, string, IDictionary<string, string>, string?, DateTime?>(
                    (_, _, _, _, t, _, _) => captured = t)
                .Returns(Task.CompletedTask);

            await _sut.ForgotPassword("j@j.com"); // queues the email carrying the token
            var token = captured!["ResetUrl"].Split("token=")[1];

            _userRepo.Setup(r => r.GetById(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.Update(It.IsAny<User>())).ReturnsAsync(user);
            _refreshTokenRepo.Setup(r => r.RevokeByUserId(1)).Returns(Task.CompletedTask);

            await _sut.ResetPassword(new ResetPasswordRequest { Token = token, NewPassword = "NewPass@1234" });

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
            _userRepo.Setup(r => r.GetByEmail("j@j.com"))
                .ReturnsAsync(new User { Id = 1, Name = "Jay", Email = "j@j.com", IsActive = true });

            IDictionary<string, string>? captured = null;
            _emailQueue.Setup(q => q.Enqueue(It.IsAny<string>(), It.IsAny<string>(), EmailTemplateKey.PasswordReset,
                    It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<DateTime?>()))
                .Callback<string, string, string, string, IDictionary<string, string>, string?, DateTime?>(
                    (_, _, _, _, t, _, _) => captured = t)
                .Returns(Task.CompletedTask);

            await _sut.ForgotPassword("j@j.com");
            var token = captured!["ResetUrl"].Split("token=")[1];

            await _sut.Invoking(s => s.ResetPassword(new ResetPasswordRequest { Token = token, NewPassword = "weak" }))
                .Should().ThrowAsync<ValidationException>();
        }

        // ── Welcome email ────────────────────────────────────────────────────────

        [Test]
        public async Task Register_ShouldEnqueueWelcomeEmail()
        {
            _userRepo.Setup(r => r.EmailExists(It.IsAny<string>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.Add(It.IsAny<User>()))
                .ReturnsAsync((User u) => { u.Id = 1; return u; });

            await _sut.Register(new RegisterRequest
            {
                Name = "Jay",
                Email = "j@j.com",
                Phone = "9999999999",
                Password = "Test@1234"
            });

            _emailQueue.Verify(q => q.Enqueue("j@j.com", "Jay", EmailTemplateKey.Welcome,
                It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<DateTime?>()),
                Times.Once);
        }
    }
}
