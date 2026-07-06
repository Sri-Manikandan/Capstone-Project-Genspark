using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    public interface IChangeLogService
    {
        Task<PagedResult<ChangeLogDto>> Search(ChangeLogQueryRequest request);
    }
}
