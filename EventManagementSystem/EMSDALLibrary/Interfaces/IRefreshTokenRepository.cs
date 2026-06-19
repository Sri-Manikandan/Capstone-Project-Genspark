using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IRefreshTokenRepository : IRepository<RefreshToken>
    {
        Task<RefreshToken?> GetByToken(string token);
        Task RevokeByUserId(int userId);
    }
}
