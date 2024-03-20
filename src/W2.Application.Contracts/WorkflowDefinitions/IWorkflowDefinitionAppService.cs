using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace W2.WorkflowDefinitions
{
    public interface IWorkflowDefinitionAppService : IApplicationService
    {
        Task<WorkflowDefinitionSummaryDto> GetByDefinitionIdAsync(string definitionId);
        Task<PagedResultDto<WorkflowDefinitionSummaryDto>> ListAllAsync();
        Task SaveWorkflowInputDefinitionAsync(WorkflowCustomInputDefinitionDto input);
        Task SaveWorkflowDefinitionSettingAsync(WorkflowCustomDefinitionSettingDto input);
        Task<string> CreateWorkflowDefinitionAsync(CreateWorkflowDefinitionDto input);
        Task DeleteAsync(string id);
    }
}
