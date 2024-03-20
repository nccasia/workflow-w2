using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace W2.WorkflowDefinitions
{
    public class WorkflowCustomDefinitionSettingDto : EntityDto<Guid>
    {
        public string WorkflowDefinitionId { get; set; }
        public Dictionary<string,string> PropertyDefinitions { get; set; }
    }

}
