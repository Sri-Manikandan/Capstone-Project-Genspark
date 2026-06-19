using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using EMSModelLibrary.DTOs;
using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EMSBLLLibrary.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRefreshTokenRepository _refreshTokenRepo;
        private readonly IConfiguration _config;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;

        private const string ResetTokenPrefix = "pwd_reset:";
        private static readonly TimeSpan ResetTokenTtl = TimeSpan.FromMinutes(15);

        public AuthService(
            IUserRepository userRepo,
            IRefreshTokenRepository refreshTokenRepo,
            IConfiguration config,
            IMapper mapper,
            IMemoryCache cache)
        {
            _userRepo = userRepo;
            _refreshTokenRepo = refreshTokenRepo;
            _config = config;
            _mapper = mapper;
            _cache = cache;
        }

        public async Task<AuthResponse> Register(RegisterRequest request)
        {
            InputValidator.ValidateName(request.Name);
            InputValidator.ValidateEmail(request.Email);
            InputValidator.ValidatePhone(request.Phone);
            InputValidator.ValidatePassword(request.Password);

            if (await _userRepo.EmailExists(request.Email))
                throw new ValidationException("Email is already registered.");

            var role = "User";

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                Phone = request.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = role,
                IsActive = true
            };

            await _userRepo.Add(user);
            return await BuildAuthResponse(user);
        }

        public async Task<AuthResponse> Login(LoginRequest request)
        {
            var user = await _userRepo.GetByEmail(request.Email)
                ?? throw new InvalidCredentialsException("Invalid email or password.");

            if (!user.IsActive)
                throw new InvalidCredentialsException("Account is deactivated.");

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new InvalidCredentialsException("Invalid email or password.");

            return await BuildAuthResponse(user);
        }

        public async Task<AuthResponse> RefreshToken(string refreshToken)
        {
            var token = await _refreshTokenRepo.GetByToken(refreshToken)
                ?? throw new InvalidCredentialsException("Invalid refresh token.");

            if (token.RevokedAt != null)
                throw new InvalidCredentialsException("Refresh token has been revoked.");

            if (token.ExpiresAt < DateTime.UtcNow)
                throw new InvalidCredentialsException("Refresh token has expired.");

            var user = await _userRepo.GetById(token.UserId)
                ?? throw new InvalidCredentialsException("User not found.");

            if (!user.IsActive)
                throw new InvalidCredentialsException("Account is deactivated.");

            token.RevokedAt = DateTime.UtcNow;
            await _refreshTokenRepo.Update(token);

            return await BuildAuthResponse(user);
        }

        public async Task Logout(string refreshToken)
        {
            var token = await _refreshTokenRepo.GetByToken(refreshToken);
            if (token != null && token.RevokedAt == null)
            {
                token.RevokedAt = DateTime.UtcNow;
                await _refreshTokenRepo.Update(token);
            }
        }

        public async Task<ForgotPasswordResponse> ForgotPassword(string email)
        {
            var user = await _userRepo.GetByEmail(email);

            // Silent success — do not reveal whether the email is registered
            if (user == null || !user.IsActive)
                return new ForgotPasswordResponse
                {
                    Message = "If that email is registered, a reset token has been generated.",
                    ResetToken = string.Empty
                };

            var token = Guid.NewGuid().ToString("N"); // 32-char hex, URL-safe
            _cache.Set($"{ResetTokenPrefix}{token}", user.Id, ResetTokenTtl);

            return new ForgotPasswordResponse
            {
                Message = "Password reset token generated. In production this would be sent via email.",
                ResetToken = token
            };
        }

        public async Task ResetPassword(ResetPasswordRequest request)
        {
            var cacheKey = $"{ResetTokenPrefix}{request.Token}";

            if (!_cache.TryGetValue(cacheKey, out int userId))
                throw new ValidationException("Invalid or expired password reset token.");

            InputValidator.ValidatePassword(request.NewPassword);

            var user = await _userRepo.GetById(userId)
                ?? throw new NotFoundException("User not found.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.Update(user);

            await _refreshTokenRepo.RevokeByUserId(userId);
            _cache.Remove(cacheKey);
        }

        private async Task<AuthResponse> BuildAuthResponse(User user)
        {
            var accessExpiry = DateTime.UtcNow.AddMinutes(GetAccessTokenExpiryMinutes());
            var rawRefresh = GenerateRefreshTokenString();
            var refreshExpiryDays = int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7");

            await _refreshTokenRepo.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = rawRefresh,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiryDays)
            });

            return new AuthResponse
            {
                AccessToken = GenerateAccessToken(user),
                RefreshToken = rawRefresh,
                AccessTokenExpiry = accessExpiry,
                User = _mapper.Map<UserDto>(user)
            };
        }

        private string GenerateAccessToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("name", user.Name),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(GetAccessTokenExpiryMinutes()),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GenerateRefreshTokenString() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        private int GetAccessTokenExpiryMinutes() =>
            int.Parse(_config["Jwt:AccessTokenExpiryMinutes"] ?? "60");
    }
}
