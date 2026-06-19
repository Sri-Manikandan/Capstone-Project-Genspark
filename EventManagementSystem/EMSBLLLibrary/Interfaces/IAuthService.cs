using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> Register(RegisterRequest request);
        Task<AuthResponse> Login(LoginRequest request);
        Task<AuthResponse> RefreshToken(string refreshToken);
        Task Logout(string refreshToken);
        Task<ForgotPasswordResponse> ForgotPassword(string email);
        Task ResetPassword(ResetPasswordRequest request);
    }
}
