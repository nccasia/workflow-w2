using Elsa.Models;
using Elsa.Persistence;
using Elsa.Persistence.Specifications;
using Elsa.Services;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using W2.Identity;
using W2.Permissions;
using W2.Specifications;

namespace W2.WorkflowDefinitions
{
    [Authorize(W2Permissions.WorkflowManagementWorkflowDefinitions)]
    public class WorkflowDefinitionAppService : W2AppService, IWorkflowDefinitionAppService
    {
        private readonly IWorkflowDefinitionStore _workflowDefinitionStore;
        private readonly IRepository<WorkflowCustomInputDefinition, Guid> _workflowCustomInputDefinitionRepository;
        public readonly IRepository<WorkflowCustomDefinitionSetting, Guid> _workflowCustomDefinitionSettingRepository;
        private readonly IWorkflowPublisher _workflowPublisher;

        public WorkflowDefinitionAppService(
            IWorkflowDefinitionStore workflowDefinitionStore,
            IRepository<WorkflowCustomInputDefinition, Guid> workflowCustomInputDefinitionRepository,
            IRepository<WorkflowCustomDefinitionSetting, Guid> workflowCustomDefinitionSettingRepository,
            IWorkflowPublisher workflowPublisher)
        {
            _workflowDefinitionStore = workflowDefinitionStore;
            _workflowCustomInputDefinitionRepository = workflowCustomInputDefinitionRepository;
            _workflowCustomDefinitionSettingRepository = workflowCustomDefinitionSettingRepository;
            _workflowPublisher = workflowPublisher;
        }

        public async Task<PagedResultDto<WorkflowDefinitionSummaryDto>> ListAllAsync()
        {
            var specification = Specification<WorkflowDefinition>.Identity;
            specification = specification.And(new ListAllWorkflowDefinitionsSpecification(CurrentTenantStrId, null));

            if (!CurrentUser.IsInRole("admin") && !CurrentUser.IsInRole(RoleNames.Designer))
            {
                specification = specification.And(new PublishedWorkflowDefinitionsSpecification());
            }

            var workflowDefinitions = (await _workflowDefinitionStore
                .FindManyAsync(
                    specification,
                    new OrderBy<WorkflowDefinition>(x => x.Name!, SortDirection.Ascending)
                ))
                .Where(w => w.IsPublished)
                .ToList();
            var definitionIds = workflowDefinitions.Select(x => x.DefinitionId).ToList();
            var inputDefinitions = await _workflowCustomInputDefinitionRepository
                .GetListAsync(x => definitionIds.Contains(x.WorkflowDefinitionId));
            var settingDefinitions = await _workflowCustomDefinitionSettingRepository
                .GetListAsync(x => definitionIds.Contains(x.WorkflowDefinitionId));
            var workflowDefinitionSummaries = ObjectMapper.Map<List<WorkflowDefinition>, List<WorkflowDefinitionSummaryDto>>(workflowDefinitions);

            foreach (var summary in workflowDefinitionSummaries)
            {
                var inputDefinition = inputDefinitions.FirstOrDefault(x => x.WorkflowDefinitionId == summary.DefinitionId);
                if (inputDefinition == null)
                {
                    continue;
                }
                summary.InputDefinition = ObjectMapper.Map<WorkflowCustomInputDefinition, WorkflowCustomInputDefinitionDto>(inputDefinition);
            }
            foreach (var summary in workflowDefinitionSummaries)
            {
                var settingDefinition = settingDefinitions.FirstOrDefault(x => x.WorkflowDefinitionId == summary.DefinitionId);
                if (settingDefinition == null)
                {
                    continue;
                }
                summary.SettingDefinition = new WorkflowCustomDefinitionSettingDto
                {
                    WorkflowDefinitionId = settingDefinition.WorkflowDefinitionId,
                    PropertyDefinitions = settingDefinition.PropertyDefinitions
                } ;
            }

            return new PagedResultDto<WorkflowDefinitionSummaryDto>(workflowDefinitionSummaries.Count, workflowDefinitionSummaries);
        }

        public async Task<WorkflowDefinitionSummaryDto> GetByDefinitionIdAsync(string definitionId)
        {
            var specification = new ListAllWorkflowDefinitionsSpecification(CurrentTenantStrId, new string[] { definitionId });
            var workflowDefinition = await _workflowDefinitionStore.FindAsync(specification);
            if (workflowDefinition == null)
            {
                throw new UserFriendlyException(L["Exception:WorkflowDefinitionNotFound"]);
            }

            var workflowDefinitionDto = ObjectMapper.Map<WorkflowDefinition, WorkflowDefinitionSummaryDto>(workflowDefinition);
            var inputDefinition = await _workflowCustomInputDefinitionRepository
                .FindAsync(x => x.WorkflowDefinitionId == workflowDefinition.DefinitionId);
            if (inputDefinition != null)
            {
                workflowDefinitionDto.InputDefinition = ObjectMapper.Map<WorkflowCustomInputDefinition, WorkflowCustomInputDefinitionDto>(inputDefinition);
            }

            return workflowDefinitionDto;
        }

        [Authorize(W2Permissions.WorkflowManagementWorkflowDefinitionsDesign)]
        public async Task SaveWorkflowInputDefinitionAsync(WorkflowCustomInputDefinitionDto input)
        {
            if (input.Id == default)
            {
                await _workflowCustomInputDefinitionRepository.InsertAsync(
                    ObjectMapper.Map<WorkflowCustomInputDefinitionDto, WorkflowCustomInputDefinition>(input)
                );
            }
            else
            {
                var inputDefinition = await _workflowCustomInputDefinitionRepository.GetAsync(input.Id);
                ObjectMapper.Map(input, inputDefinition);
                await _workflowCustomInputDefinitionRepository.UpdateAsync(inputDefinition);
            }
        }

        [Authorize(W2Permissions.WorkflowManagementWorkflowDefinitionsDesign)]
        public async Task SaveWorkflowDefinitionSettingAsync(WorkflowCustomDefinitionSettingDto input)
        {
            if (input.Id == default)
            {
                await _workflowCustomDefinitionSettingRepository.InsertAsync(
                    ObjectMapper.Map<WorkflowCustomDefinitionSettingDto, WorkflowCustomDefinitionSetting>(input)
                );
            }
            else
            {
                var settingDefinition = await _workflowCustomDefinitionSettingRepository.GetAsync(input.Id);
                ObjectMapper.Map(input, settingDefinition);
                await _workflowCustomDefinitionSettingRepository.UpdateAsync(settingDefinition);
            }
        }

        [Authorize(W2Permissions.WorkflowManagementWorkflowDefinitionsDesign)]
        public async Task<string> CreateWorkflowDefinitionAsync(CreateWorkflowDefinitionDto input)
        {
            var workflowDefinition = ObjectMapper.Map<CreateWorkflowDefinitionDto, WorkflowDefinition>(input);

            await _workflowPublisher.SaveDraftAsync(workflowDefinition);

            return workflowDefinition.Id;
        }

        [Authorize(W2Permissions.WorkflowManagementWorkflowDefinitionsDesign)]
        public async Task DeleteAsync(string id)
        {
            await _workflowPublisher.DeleteAsync(id, VersionOptions.All);
        }
    }
}
