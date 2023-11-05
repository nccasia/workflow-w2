using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace W2.Tasks
{
    public class W2Task : CreationAuditedEntity<Guid>, IMultiTenant
    {
        public string WorkflowInstanceId { get; set; } = default!;
        public string WorkflowDefinitionId { get; set; } = default!;
        public string Email { get; set; }
        public Dictionary<string, string> Data { get; set; }
        public Guid? TenantId { get; set; }
        public Guid Author { get; set; }
        public W2TaskStatus Status { get; set; }
        public string Name { get; set; } // Task name
        public string Title { get; set; } // Task title
        public string Description { get; set; } // Task description
        public string Reason { get; set; } // Task reason
        public string ApproveSignal { get; set; } // Task when approving
        public string RejectSignal { get; set; } // Task when rejecting
        public string DynamicActionData { get; set; } // Dynamic action data for task
        public List<string> OtherActionSignals { get; set; } // List of other action signals for Task
        public string UpdatedBy { get; set; } // Email which updated the task
    }
}
