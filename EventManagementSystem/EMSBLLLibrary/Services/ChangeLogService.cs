using AutoMapper;
using EMSBLLLibrary.Interfaces;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Services
{
    public class ChangeLogService : IChangeLogService
    {
        private readonly IChangeLogRepository _changeLogRepo;
        private readonly IMapper _mapper;

        public ChangeLogService(IChangeLogRepository changeLogRepo, IMapper mapper)
        {
            _changeLogRepo = changeLogRepo;
            _mapper = mapper;
        }

        public async Task<PagedResult<ChangeLogDto>> Search(ChangeLogQueryRequest request)
        {
            var (items, total) = await _changeLogRepo.SearchPaged(
                request.EntityName, request.UserId, request.Action, request.Page, request.PageSize);

            return new PagedResult<ChangeLogDto>
            {
                Items = _mapper.Map<List<ChangeLogDto>>(items),
                TotalCount = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
    }
}
