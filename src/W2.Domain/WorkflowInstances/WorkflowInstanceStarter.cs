using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace W2.WorkflowInstances
{
    public class WorkflowInstanceStarter : CreationAuditedEntity<Guid>, IMultiTenant
    {
        public string WorkflowInstanceId { get; set; } = default!;
        public Dictionary<string, string> Input { get; set; }
        public Guid? TenantId { get; set; }
    }
}
