using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetByEmail(string email);
        Task<bool> EmailExists(string email);
        Task<(List<User> Items, int TotalCount)> Search(string? query, string? role, bool? isActive, int page, int pageSize);
    }
}
