using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace W2.WorkflowDefinitions
{
    public class WorkflowCustomDefinitionSetting : CreationAuditedEntity<Guid>, IMultiTenant
    {
        public string WorkflowDefinitionId { get; set; }
        public Dictionary<string,string> PropertyDefinitions { get; set; }
        public Guid? TenantId { get; set; }
    }
}
