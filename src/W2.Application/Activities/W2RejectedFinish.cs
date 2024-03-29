﻿using Elsa.Activities.ControlFlow;
using Elsa.ActivityResults;
using Elsa.Attributes;
using Elsa.Models;
using Elsa.Services.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using W2.Tasks;
using W2.WorkflowInstances;

namespace W2.Activities
{
    [Activity(
        Category = "Workflows",
        DisplayName = "W2 Rejected Finish",
        Description = "Update Assign Task Status, Workflow Status and Removes any blocking activities from the current container (workflow or composite activity).",
        Outcomes = new string[] { })]
    public class W2RejectedFinish : Finish
    {
        private IRepository<W2Task, Guid> _taskRepository;
        private readonly IRepository<WorkflowInstanceStarter, Guid> _instanceStarterRepository;

        public W2RejectedFinish(
            IRepository<W2Task, Guid> taskRepository,
            IRepository<WorkflowInstanceStarter, Guid> instanceStarterRepository)
        {
            _taskRepository = taskRepository;
            _instanceStarterRepository = instanceStarterRepository;
        }

        protected async override ValueTask<IActivityExecutionResult> OnExecuteAsync(ActivityExecutionContext context)
        {
            // update status for workflow task
            var tasksToApprove = await _taskRepository
                .GetListAsync(x => x.WorkflowInstanceId == context.WorkflowInstance.Id.ToString() && x.Status == W2TaskStatus.Pending);

            tasksToApprove.ForEach(task =>
            {
                task.Status = W2TaskStatus.Reject;
                task.UpdatedBy = "W2 Workflow";
            });

            await _taskRepository.UpdateManyAsync(tasksToApprove);

            // update status for workflow instance stater 
            var myWorkflow = await _instanceStarterRepository.FirstOrDefaultAsync(x => x.WorkflowInstanceId == context.WorkflowInstance.Id.ToString());
            myWorkflow.Status = WorkflowInstancesStatus.Rejected;
            await _instanceStarterRepository.UpdateAsync(myWorkflow);

            // Handler Blocking Activities
            await WorkflowUtility.ProcessBlockingActivitiesAsync(context);

            // Handler Scope Activities
            await WorkflowUtility.ProcessScopeActivitiesAsync(context);

            // Finish process
            Output = new FinishOutput(ActivityOutput, OutcomeNames);
            context.LogOutputProperty(this, "Output", Output);

            ICompositeActivityBlueprint parentBlueprint = context.ActivityBlueprint.Parent;
            bool isRoot = parentBlueprint == null;
            if (isRoot)
            {
                context.WorkflowExecutionContext.ClearScheduledActivities();
            }

            return Noop();
        }
    }
}
