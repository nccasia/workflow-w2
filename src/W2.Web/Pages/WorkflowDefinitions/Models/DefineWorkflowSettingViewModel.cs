using System;
using System.Collections.Generic;

namespace W2.Web.Pages.WorkflowDefinitions.Models
{
    public class DefineWorkflowSettingViewModel
    {
        public Guid? Id { get; set; }
        public string WorkflowDefinitionId { get; set; }
        public Dictionary<string,string> PropertyDefinitionViewModels { get; set; } 
    }
}
