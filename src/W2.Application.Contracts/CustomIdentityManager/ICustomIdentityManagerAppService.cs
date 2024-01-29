using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using W2.Tasks;

namespace W2.CustomIdentityManager
{
    public interface ICustomIdentityManagerAppService: IApplicationService
    {
        Task<PagedResultDto<CustomUserManageDto>> GetListAsync(ListUsersInput input);
    }
}
