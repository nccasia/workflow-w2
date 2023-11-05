using Elsa.Activities.Http.Events;
using Elsa.Activities.Signaling.Models;
using Elsa.Activities.Signaling.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Open.Linq.AsyncExtensions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using Elsa.Persistence;
using W2.Specifications;
using Elsa;
using Newtonsoft.Json;
using Volo.Abp.Identity;
using W2.TaskEmail;
using W2.TaskActions;
using System.Collections;
using Elsa.Models;
using W2.ExternalResources;

namespace W2.Tasks
{
    [Authorize]
    public class TaskAppService : W2AppService, ITaskAppService, ITaskEmailService
    {
        private readonly IRepository<W2Task, Guid> _taskRepository;
        private readonly IRepository<W2TaskEmail, Guid> _taskEmailRepository;
        private readonly IRepository<W2TaskActions, Guid> _taskActionsRepository;
        private readonly IIdentityUserRepository _userRepository;
        private readonly ISignaler _signaler;
        private readonly IMediator _mediator;
        private readonly ICurrentUser _currentUser;
        private readonly IWorkflowInstanceStore _workflowInstanceStore;
        private readonly IWorkflowDefinitionStore _workflowDefinitionStore;


        public TaskAppService(
            IRepository<W2Task, Guid> taskRepository,
            IRepository<W2TaskEmail, Guid> taskEmailRepository,
            IRepository<W2TaskActions, Guid> taskActionsRepository,
            ISignaler signaler,
            IMediator mediator,
            ICurrentUser currentUser,
            IWorkflowInstanceStore workflowInstanceStore,
            IWorkflowDefinitionStore workflowDefinitionStore,
            IIdentityUserRepository userRepository)
        {
            _signaler = signaler;
            _taskRepository = taskRepository;
            _taskEmailRepository = taskEmailRepository;
            _taskActionsRepository = taskActionsRepository;
            _mediator = mediator;
            _currentUser = currentUser;
            _workflowInstanceStore = workflowInstanceStore;
            _workflowDefinitionStore = workflowDefinitionStore;
            _userRepository = userRepository;
        }

        [AllowAnonymous]
        [RemoteService(IsEnabled = false)]
        public async Task<string> assignTask(AssignTaskInput input)
        {
            var workflowInstance = await _workflowInstanceStore.FindByIdAsync(input.WorkflowInstanceId);
            var workflowDefinitions = (await _workflowDefinitionStore.FindManyAsync(
                new ListAllWorkflowDefinitionsSpecification(CurrentTenantStrId, new string[] { workflowInstance.DefinitionId }))).FirstOrDefault();
            string taskTitle = null;

            if (!string.IsNullOrEmpty(input.TaskTitle))
            {
                taskTitle = input.TaskTitle;
            }

            var task = await _taskRepository.InsertAsync(new W2Task
            {
                TenantId = CurrentTenant.Id,
                Author = input.UserId,
                WorkflowInstanceId = input.WorkflowInstanceId,
                WorkflowDefinitionId = workflowInstance.DefinitionId,
                DynamicActionData = input.DynamicActionData,
                Status = W2TaskStatus.Pending,
                Name = workflowDefinitions.Name,
                Title = taskTitle,
                Description = input.Description,
                ApproveSignal = input.ApproveSignal,
                RejectSignal = input.RejectSignal,
            });

            if (input.OtherActionSignals != null)
            {
                foreach (string action in input.OtherActionSignals)
                {
                    await _taskActionsRepository.InsertAsync(
                        new W2TaskActions
                        {
                            OtherActionSignal = action,
                            Status = W2TaskActionsStatus.Pending,
                            TaskId = task.Id.ToString(),
                        }
                    );
                }
            }


            foreach (string email in input.EmailTo)
            {
                await _taskEmailRepository.InsertAsync(new W2TaskEmail
                {
                    Email = email,
                    TaskId = task.Id.ToString(),
                });
            }

            return task.Id.ToString();
        }

        public async Task createTask(string id) { }

        public async Task<string> ApproveAsync(ApproveTasksInput input)
        {
            var myTask = await _taskRepository.FirstOrDefaultAsync(x => x.Id == Guid.Parse(input.Id));
            if (myTask == null || myTask.Status != W2TaskStatus.Pending)
            {
                throw new UserFriendlyException(L["Exception:MyTaskNotValid"]);
            }

            var taskEmail = (await _taskEmailRepository.GetListAsync(x => x.TaskId == myTask.Id.ToString()))
                    .Where(x => x.Email == _currentUser.Email && x.TaskId == myTask.Id.ToString())
                    .ToList().FirstOrDefault();

            if (taskEmail == null)
            {
                throw new UserFriendlyException(L["Exception:No Permission"]);
            }

            var Inputs = new Dictionary<string, string>
            {
                { "Reason", $"{myTask.ApproveSignal}" },
                { "TriggeredBy", $"{_currentUser.Email}" }
            };

            if (!string.IsNullOrEmpty(input.DynamicActionData))
            {
                myTask.DynamicActionData = input.DynamicActionData;
            }

            var affectedWorkflows = await _signaler.TriggerSignalAsync(myTask.ApproveSignal, Inputs, myTask.WorkflowInstanceId).ToList();
            var signal = new SignalModel(myTask.ApproveSignal, myTask.WorkflowInstanceId);
            await _mediator.Publish(new HttpTriggeredSignal(signal, affectedWorkflows));

            myTask.Status = W2TaskStatus.Approve;
            myTask.UpdatedBy = _currentUser.Email;
            await _taskRepository.UpdateAsync(myTask);

            return "Approval successful";
        }

        public async Task<string> RejectAsync([Required] string id, [Required] string reason)
        {
            var myTask = await _taskRepository.FirstOrDefaultAsync(x => x.Id == Guid.Parse(id));
            if (myTask == null || myTask.Status != W2TaskStatus.Pending)
            {
                throw new UserFriendlyException(L["Exception:MyTaskNotValid"]);
            }

            var taskEmail = (await _taskEmailRepository.GetListAsync(x => x.TaskId == myTask.Id.ToString()))
                    .Where(x => x.Email == _currentUser.Email && x.TaskId == myTask.Id.ToString())
                    .ToList().FirstOrDefault();

            if (taskEmail == null)
            {
                throw new UserFriendlyException(L["Exception:No Permission"]);
            }

            var Inputs = new Dictionary<string, string>
            {
                { "Reason", $"{reason}" },
                { "TriggeredBy", $"{_currentUser.Email}" }
            };

            var affectedWorkflows = await _signaler.TriggerSignalAsync(myTask.RejectSignal, Inputs, myTask.WorkflowInstanceId).ToList();

            var signal = new SignalModel(myTask.RejectSignal, myTask.WorkflowInstanceId);
            await _mediator.Publish(new HttpTriggeredSignal(signal, affectedWorkflows));

            myTask.Status = W2TaskStatus.Reject;
            myTask.UpdatedBy = _currentUser.Email;
            myTask.Reason = reason;

            await _taskRepository.UpdateAsync(myTask);

            return "Reject successful";
        }

        public async Task<string> ActionAsync(ListTaskActions input)
        {
            var myTask = await _taskRepository.FirstOrDefaultAsync(x => x.Id == Guid.Parse(input.Id));
            if (myTask == null || myTask.Status != W2TaskStatus.Pending)
            {
                throw new UserFriendlyException(L["Exception:MyTaskNotValid"]);
            }

            var taskEmail = (await _taskEmailRepository.GetListAsync(x => x.TaskId == myTask.Id.ToString()))
                    .Where(x => x.Email == _currentUser.Email && x.TaskId == myTask.Id.ToString())
                    .ToList().FirstOrDefault();

            if (taskEmail == null)
            {
                throw new UserFriendlyException(L["Exception:No Permission"]);
            }

            var actions = await _taskActionsRepository.FirstOrDefaultAsync(x => x.TaskId == myTask.Id.ToString() && x.OtherActionSignal == input.Action);
            if (actions == null || actions.Status != W2TaskActionsStatus.Pending)
            {
                throw new UserFriendlyException(L["Exception:Action is not valid for this task."]);
            }

            var Inputs = new Dictionary<string, string>
            {
                { "Reason", $"{input.Action}" },
                { "TriggeredBy", $"{_currentUser.Email}" }
            };

            var affectedWorkflows = await _signaler.TriggerSignalAsync(input.Action, Inputs, myTask.WorkflowInstanceId).ToList();

            var signal = new SignalModel(input.Action, myTask.WorkflowInstanceId);
            await _mediator.Publish(new HttpTriggeredSignal(signal, affectedWorkflows));

            actions.Status = W2TaskActionsStatus.Approve;
            await _taskActionsRepository.UpdateAsync(actions);

            return "Send Action successful";
        }

        public async Task<PagedResultDto<W2TasksDto>> ListAsync(ListTaskstInput input)
        {
            var users = await _userRepository.GetListAsync();
            var tasks = await _taskRepository.GetListAsync();
            var taskEmail = await _taskEmailRepository.GetListAsync();
            var taskAction = await _taskActionsRepository.GetListAsync();
            var hasWorkflowDefinitionId = !string.IsNullOrEmpty(input.WorkflowDefinitionId);

            var query = from task in tasks
                        join user in users on task.Author equals user.Id
                        join email in taskEmail on new { TaskID = task.Id.ToString() } equals new { TaskID = email.TaskId }
                        join action in taskAction on new { TaskID = task.Id.ToString() } equals new { TaskID = action.TaskId } into actionGroup
                        let emailList = (
                            from email in taskEmail
                            where email.TaskId == task.Id.ToString()
                            select email.Email
                        ).ToList()
                        let actionList = (
                            from action in actionGroup.DefaultIfEmpty()
                            select action != null ? new TaskActionsDto
                            {
                                OtherActionSignal = action.OtherActionSignal,
                                Status = action.Status
                            } : null
                        ).OrderBy(action => action?.OtherActionSignal).ToList()
                        select new
                        {
                            W2TaskEmail = email,
                            W2User = user,
                            W2task = task,
                            EmailTo = emailList,
                            OtherActionSignals = actionList.All(a => a != null) ? actionList : null
                        };


            List<Func<W2Task, bool>> checks = new List<Func<W2Task, bool>>();
            var isAdmin = _currentUser.IsInRole("admin");
            if (!isAdmin)
            {
                query = query.Where(x => x.W2TaskEmail.Email.Contains(_currentUser.Email));
            }

            if (!string.IsNullOrWhiteSpace(input.EmailRequest))
            {
                string emailRequest = input.EmailRequest.Trim();
                query = query.Where(x => x.W2User.Email.Contains(emailRequest));
            }

            if (!string.IsNullOrWhiteSpace(input.EmailAssign) && isAdmin)
            {
                string emailAssign = input.EmailAssign.Trim();
                query = query.Where(x => x.W2TaskEmail.Email.Contains(emailAssign));
            }


            if (!input.Dates.IsNullOrWhiteSpace())
            {
                query = query.Where(x => new DateTimeOffset(x.W2task.CreationTime).ToUnixTimeSeconds() >= DateTimeOffset.Parse(input.Dates).ToUnixTimeSeconds());
            }

            if (input.Status != null)
            {
                query = query.Where(x => input.Status.Contains(x.W2task.Status));
            }

            if (hasWorkflowDefinitionId)
            {
                query = query.Where(x => x.W2task.WorkflowDefinitionId == input.WorkflowDefinitionId);
            }

            var totalItemCount = query
                .GroupBy(x => x.W2task.Id)
                .Select(group => group.FirstOrDefault())
                .Count();

            var requestTasks = query
                .GroupBy(x => x.W2task.Id)
                .Select(group => group.FirstOrDefault())
                .OrderByDescending(task => task.W2task.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount).Select(x => new W2TasksDto
                {
                    Author = x.W2task.Author,
                    AuthorName = x.W2User.Name,
                    CreationTime = x.W2task.CreationTime,
                    Description = x.W2task.Description,
                    Email = x.W2task.Email,
                    Id = x.W2task.Id,
                    Name = x.W2task.Name,
                    Title = x.W2task.Title,
                    EmailTo = x.EmailTo,
                    DynamicActionData = x.W2task.DynamicActionData,
                    OtherActionSignals = x.OtherActionSignals,
                    Reason = x.W2task.Reason,
                    Status = x.W2task.Status,
                    WorkflowDefinitionId = x.W2task.WorkflowDefinitionId,
                    WorkflowInstanceId = x.W2task.WorkflowInstanceId
                })
                .ToList();

            return new PagedResultDto<W2TasksDto>(totalItemCount, requestTasks);
        }

        public async Task<TaskDetailDto> GetDetailByIdAsync(string id)
        {
            var myTask = await _taskRepository.FirstOrDefaultAsync(x => x.Id == Guid.Parse(id));
            var taskAction = await _taskActionsRepository.GetListAsync(x => x.TaskId == myTask.Id.ToString());
            var taskEmail = await _taskEmailRepository.GetListAsync();
            var query = from task in new List<W2Task> { myTask }
                        join email in taskEmail on new { TaskID = task.Id.ToString() } equals new { TaskID = email.TaskId }
                        join action in taskAction on task.Id.ToString() equals action.TaskId into actionGroup
                        let emailList = (
                             from email in taskEmail
                             where email.TaskId == task.Id.ToString()
                             select email.Email
                         ).ToList()
                        let actionList = (
                             from action in actionGroup.DefaultIfEmpty()
                             select action != null ? new TaskActionsDto
                             {
                                 OtherActionSignal = action.OtherActionSignal,
                                 Status = action.Status
                             } : null
                         ).OrderBy(action => action?.OtherActionSignal).ToList()
                        select new
                        {
                            W2task = task,
                            EmailTo = emailList,
                            OtherActionSignals = actionList.All(a => a != null) ? actionList : null
                        };


            var workflowInstanceId = myTask.WorkflowInstanceId;
            var workflowInstance = await _workflowInstanceStore.FindByIdAsync(workflowInstanceId);

            var data = workflowInstance.Variables.Data;

            var taskDto = ObjectMapper.Map<W2Task, W2TasksDto>(query.FirstOrDefault()?.W2task);
            var taskDetailDto = new TaskDetailDto
            {
                Tasks = taskDto,
                OtherActionSignals = query.FirstOrDefault()?.OtherActionSignals,
                EmailTo = query.FirstOrDefault()?.EmailTo,
                Input = data,
            };

            return taskDetailDto;
        }

        public async Task<PagedResultDto<W2TasksDto>> DynamicDataByIdAsync(TaskDynamicDataInput input)
        {
            var query = await _taskRepository.GetListAsync(x => x.WorkflowInstanceId == input.WorkflowInstanceId);
            var totalItemCount = query
                .GroupBy(x => x.Id)
                .Select(group => group.FirstOrDefault())
                .Count();
            var tasks = query
                .GroupBy(x => x.Id)
                .Select(group => group.FirstOrDefault())
                .OrderByDescending(task => task.CreationTime)
                .Select(x => new W2TasksDto
                {
                    Author = x.Author,
                    CreationTime = x.CreationTime,
                    Description = x.Description,
                    Email = x.Email,
                    Id = x.Id,
                    Name = x.Name,
                    Title = x.Title,
                    DynamicActionData = x.DynamicActionData,
                    Reason = x.Reason,
                    Status = x.Status,
                    WorkflowDefinitionId = x.WorkflowDefinitionId,
                    WorkflowInstanceId = x.WorkflowInstanceId
                })
                .ToList();
            return new PagedResultDto<W2TasksDto>(totalItemCount, tasks);
        }

        [AllowAnonymous]
        [RemoteService(IsEnabled = false)]
        public async Task<Dictionary<string, string>> handleDynamicData(TaskDynamicDataInput input)
        {
            List<W2TasksDto> tasks = (List<W2TasksDto>)(await DynamicDataByIdAsync(input)).Items;
            Dictionary<string, string> dynamicData = new Dictionary<string, string>();

            foreach (var task in tasks)
            {
                var dynamicActionData = task.DynamicActionData;

                if(dynamicActionData.IsNullOrEmpty())
                {
                    continue;
                }

                try
                {
                    List<Dictionary<string, object>> data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(dynamicActionData);
                    UpdateDynamicData(dynamicData, data);
                } catch (Exception)
                {
                    continue;
                }
            }

            dynamicData = dynamicData.ToDictionary(
                item => item.Key, 
                item => item.Value.Replace("\n", "</p><p>") + "</p>"
            );

            return dynamicData;
        }

        private void UpdateDynamicData(Dictionary<string, string> dynamicData, List<Dictionary<string, object>> data)
        {
            foreach (var item in data)
            {
                string name = item["name"].ToString();
                string itemData = item["data"].ToString();

                if (dynamicData.ContainsKey(name))
                {
                    dynamicData[name] += itemData;
                }
                else
                {
                    dynamicData[name] = "<p>" + itemData + "\n";
                }
            }
        }
    }
}