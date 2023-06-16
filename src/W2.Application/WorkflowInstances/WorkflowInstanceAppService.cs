﻿using Elsa.Models;
using Elsa.Persistence;
using Elsa.Persistence.Specifications;
using Elsa.Persistence.Specifications.WorkflowInstances;
using Elsa.Services;
using Elsa.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.TextTemplating;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using W2.ExternalResources;
using W2.Permissions;
using W2.Specifications;
using W2.Templates;
using W2.WorkflowInstanceStates;

namespace W2.WorkflowInstances
{
    [Authorize(W2Permissions.WorkflowManagementWorkflowInstances)]
    public class WorkflowInstanceAppService : W2AppService, IWorkflowInstanceAppService
    {
        private readonly IWorkflowLaunchpad _workflowLaunchpad;
        private readonly IRepository<WorkflowInstanceStarter, Guid> _instanceStarterRepository;
        private readonly IWorkflowInstanceStore _workflowInstanceStore;
        private readonly IWorkflowDefinitionStore _workflowDefinitionStore;
        private readonly IWorkflowInstanceCanceller _canceller;
        private readonly IWorkflowInstanceDeleter _workflowInstanceDeleter;
        private readonly IEmailSender _emailSender;
        private readonly ITemplateRenderer _templateRenderer;
        private readonly ILogger<WorkflowInstanceAppService> _logger;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IRepository<IdentityUser> _userRepository;
        private readonly WorkflowInstanceStarterManager _workflowInstanceStarterManager;

        public WorkflowInstanceAppService(IWorkflowLaunchpad workflowLaunchpad,
            IRepository<WorkflowInstanceStarter, Guid> instanceStarterRepository,
            IWorkflowInstanceStore workflowInstanceStore,
            IWorkflowDefinitionStore workflowDefinitionStore,
            IWorkflowInstanceCanceller canceller,
            IWorkflowInstanceDeleter workflowInstanceDeleter,
            IEmailSender emailSender,
            ITemplateRenderer templateRenderer,
            ILogger<WorkflowInstanceAppService> logger,
            IUnitOfWorkManager unitOfWorkManager,
            IRepository<IdentityUser, Guid> userRepository,
            WorkflowInstanceStarterManager workflowInstanceStarterManager)
        {
            _workflowLaunchpad = workflowLaunchpad;
            _instanceStarterRepository = instanceStarterRepository;
            _workflowInstanceStore = workflowInstanceStore;
            _workflowDefinitionStore = workflowDefinitionStore;
            _canceller = canceller;
            _workflowInstanceDeleter = workflowInstanceDeleter;
            _emailSender = emailSender;
            _templateRenderer = templateRenderer;
            _logger = logger;
            _unitOfWorkManager = unitOfWorkManager;
            _userRepository = userRepository;
            _workflowInstanceStarterManager = workflowInstanceStarterManager;
        }

        public async Task CancelAsync(string id)
        {
            var cancelResult = await _canceller.CancelAsync(id);

            switch (cancelResult.Status)
            {
                case CancelWorkflowInstanceResultStatus.NotFound:
                    throw new UserFriendlyException(L["Exception:InstanceNotFound"]);
                case CancelWorkflowInstanceResultStatus.InvalidStatus:
                    throw new UserFriendlyException(L["Exception:CancelInstanceInvalidStatus", cancelResult.WorkflowInstance!.WorkflowStatus]);
            }
        }

        [Authorize(W2Permissions.WorkflowManagementWorkflowInstancesCreate)]
        public async Task<string> CreateNewInstanceAsync(CreateNewWorkflowInstanceDto input)
        {
            var startableWorkflow = await _workflowLaunchpad.FindStartableWorkflowAsync(input.WorkflowDefinitionId, tenantId: CurrentTenantStrId);

            if (startableWorkflow == null)
            {
                throw new UserFriendlyException(L["Exception:NoStartableWorkflowFound"]);
            }

            var httpRequestModel = GetHttpRequestModel(nameof(HttpMethod.Post), input.Input);

            var executionResult = await _workflowLaunchpad.ExecuteStartableWorkflowAsync(startableWorkflow, new WorkflowInput(httpRequestModel));

            var instance = executionResult.WorkflowInstance;
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
            {
                var workflowInstanceStarter = new WorkflowInstanceStarter
                {
                    WorkflowInstanceId = instance.Id,
                    Input = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(input.Input)),
                    States = new List<WorkflowInstanceState>(),
                    FinalStatus = WorkflowFinalStatus.None
                };

                var workflowDefinition = await _workflowDefinitionStore.FindAsync(new FindWorkflowDefinitionByIdSpecification(input.WorkflowDefinitionId));

                await _instanceStarterRepository.InsertAsync(workflowInstanceStarter);
                await _workflowInstanceStarterManager.UpdateWorkflowStateAsync(workflowInstanceStarter, instance, workflowDefinition);
                
                await uow.CompleteAsync();
                _logger.LogInformation("Saved changes to database");
            }

            return instance.Id;
        }

        public async Task DeleteAsync(string id)
        {
            var result = await _workflowInstanceDeleter.DeleteAsync(id);
            if (result.Status == DeleteWorkflowInstanceResultStatus.NotFound)
            {
                throw new UserFriendlyException(L["Exception:InstanceNotFound"]);
            }
        }

        public async Task<WorkflowInstanceDto> GetByIdAsync(string id)
        {
            var specification = new WorkflowInstanceIdsSpecification(new[] { id });
            var instance = await _workflowInstanceStore.FindAsync(specification);
            if (instance == null)
            {
                throw new UserFriendlyException(L["Exception:InstanceNotFound"]);
            }

            var instanceDto = ObjectMapper.Map<WorkflowInstance, WorkflowInstanceDto>(instance);
            _logger.LogInformation("Fetch WorkflowInstanceDto begin");
            var workflowInstanceStarter = await _instanceStarterRepository.FirstOrDefaultAsync(x => x.WorkflowInstanceId == id);
            if (workflowInstanceStarter != null)
            {
                instanceDto.CreatorId = workflowInstanceStarter.CreatorId;
            }
            else
            {
                _logger.LogInformation("Workflow not found");
            }

            return instanceDto;
        }

        public async Task<PagedResultDto<WorkflowInstanceDto>> ListAsync(ListAllWorkflowInstanceInput input)
        {
            var specialStatus = new string[] { "Approved", "Rejected" };
            var specification = Specification<WorkflowInstance>.Identity;
            if (CurrentTenant.IsAvailable)
            {
                specification = specification.WithTenant(CurrentTenantStrId);
            }
            if (!string.IsNullOrWhiteSpace(input?.WorkflowDefinitionId))
            {
                specification = specification.WithWorkflowDefinition(input.WorkflowDefinitionId);
            }
            if (!string.IsNullOrWhiteSpace(input?.Status))
            {
                if (!specialStatus.Contains(input.Status))
                {
                    specification = specification.WithStatus((WorkflowStatus)Enum.Parse(typeof(WorkflowStatus), input.Status, true));
                }
                else
                {
                    specification = specification.WithStatus(WorkflowStatus.Finished);
                }
            }

            var workflowInstanceStarters = new List<WorkflowInstanceStarter>();
            if (!await AuthorizationService.IsGrantedAsync(W2Permissions.WorkflowManagementWorkflowInstancesViewAll))
            {
                workflowInstanceStarters = await _instanceStarterRepository.GetListAsync(x =>
                                                        x.CreatorId == CurrentUser.Id &&
                                                        (!specialStatus.Contains(input.Status) || x.FinalStatus == (WorkflowFinalStatus)Enum.Parse(typeof(WorkflowFinalStatus), input.Status, true))
                                                      , includeDetails: true);
                specification = specification.And(new ListAllWorkflowInstanceSpecification(workflowInstanceStarters.Select(x => x.WorkflowInstanceId).Distinct().ToArray()));
            }
            else if (specialStatus.Contains(input.Status) || !string.IsNullOrWhiteSpace(input.StakeHolder))
            {
                workflowInstanceStarters = await _instanceStarterRepository.GetListAsync(x =>
                                                        (string.IsNullOrWhiteSpace(input.StakeHolder) || x.States.SelectMany(x => x.StakeHolders).Any(x => x.User.Name.ToLower().Contains(input.StakeHolder.ToLower()))) &&
                                                        (!specialStatus.Contains(input.Status) || x.FinalStatus == (WorkflowFinalStatus)Enum.Parse(typeof(WorkflowFinalStatus), input.Status, true))
                                                      , includeDetails: true);
                specification = specification.And(new ListAllWorkflowInstanceSpecification(workflowInstanceStarters.Select(x => x.WorkflowInstanceId).Distinct().ToArray()));
            }

            var orderBySpecification = NormalizeSorting(input.Sorting);
            var pagingSpecification = Paging.Create(input.SkipCount, input.MaxResultCount);

            var instances = await _workflowInstanceStore.FindManyAsync(specification, orderBySpecification, pagingSpecification);
            var totalCount = await _workflowInstanceStore.CountAsync(specification);
            var workflowDefinitions = (await _workflowDefinitionStore.FindManyAsync(
                new ListAllWorkflowDefinitionsSpecification(CurrentTenantStrId, instances.Select(i => i.DefinitionId).Distinct().ToArray())
            )).ToList();

            var instancesIds = instances.Select(x => x.Id);
            if (!await AuthorizationService.IsGrantedAsync(W2Permissions.WorkflowManagementWorkflowInstancesViewAll) || specialStatus.Contains(input.Status) || !string.IsNullOrWhiteSpace(input.StakeHolder))
            {
                workflowInstanceStarters = workflowInstanceStarters
                                .Where(x => instancesIds.Contains(x.WorkflowInstanceId)).ToList();
            }
            else
            {
                workflowInstanceStarters = await _instanceStarterRepository.GetListAsync(x => instancesIds.Contains(x.WorkflowInstanceId), includeDetails: true);
            }

            var requestUserIds = workflowInstanceStarters.Select(x => (Guid)x.CreatorId);
            var requestUsers = await _userRepository.GetListAsync(x => requestUserIds.Contains(x.Id));

            var result = new List<WorkflowInstanceDto>();
            foreach (var instance in instances)
            {
                var workflowDefinition = workflowDefinitions.FirstOrDefault(x => x.DefinitionId == instance.DefinitionId);
                var workflowInstanceDto = ObjectMapper.Map<WorkflowInstance, WorkflowInstanceDto>(instance);
                workflowInstanceDto.WorkflowDefinitionDisplayName = workflowDefinition.DisplayName;
                workflowInstanceDto.CurrentStates = new List<string>();
                workflowInstanceDto.StakeHolders = new List<string>();

                var workflowInstanceStarter = workflowInstanceStarters.FirstOrDefault(x => x.WorkflowInstanceId == instance.Id);
                if (workflowInstanceStarter is not null)
                {
                    var identityUser = requestUsers.FirstOrDefault(x => x.Id == workflowInstanceStarter.CreatorId.Value);

                    if (identityUser != null)
                    {
                        workflowInstanceDto.UserRequestName = identityUser.Name;
                    }

                    workflowInstanceDto.Status = workflowInstanceStarter.FinalStatus != WorkflowFinalStatus.None ? workflowInstanceStarter.FinalStatus.ToString() : workflowInstanceDto.Status;
                    workflowInstanceDto.CurrentStates = workflowInstanceStarter.States.Select(x => x.StateName).ToList();
                    workflowInstanceDto.StakeHolders = workflowInstanceStarter.States.SelectMany(x => x.StakeHolders).Select(x => x.User.Name).ToList();                   
                    if (workflowInstanceDto.CurrentStates.Any(x => x.Contains("IT")))
                    {
                        workflowInstanceDto.StakeHolders.Add("IT Department");
                    }

                    if (workflowInstanceDto.CurrentStates.Any(x => x.Contains("Customer")))
                    {
                        workflowInstanceDto.StakeHolders.Add("Sale Department");
                    }
                }
                
                result.Add(workflowInstanceDto);
            }

            return new PagedResultDto<WorkflowInstanceDto>(totalCount, result);
        }

        private OrderBy<WorkflowInstance> NormalizeSorting(string sorting)
        {
            if (sorting.IsNullOrEmpty())
            {
                return new OrderBy<WorkflowInstance>(x => x.CreatedAt, SortDirection.Descending);
            }

            var sortingPart = sorting.Trim().Split(' ');
            var property = sortingPart[0].ToLower();
            var direction = sortingPart[1].ToLower();
            switch (property)
            {
                case "createdat":
                    if (direction == "asc")
                    {
                        return new OrderBy<WorkflowInstance>(x => x.CreatedAt, SortDirection.Ascending);
                    }
                    return new OrderBy<WorkflowInstance>(x => x.CreatedAt, SortDirection.Descending);
                case "lastexecutedat":
                    if (direction == "asc")
                    {
                        return new OrderBy<WorkflowInstance>(x => x.LastExecutedAt, SortDirection.Ascending);
                    }
                    return new OrderBy<WorkflowInstance>(x => x.LastExecutedAt, SortDirection.Descending);
            }

            return new OrderBy<WorkflowInstance>(x => x.CreatedAt, SortDirection.Descending);
        }
    }
}
