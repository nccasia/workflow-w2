using Volo.Abp.Application.Dtos;

namespace W2.WorkflowDefinitions
{
    public class WorkflowDefinitionSummaryDto : EntityDto<string>
    {
        public string DefinitionId { get; set; } = default!;
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int Version { get; set; }
        public bool IsSingleton { get; set; }
        public bool IsPublished { get; set; }       
        public bool IsLatest { get; set; }
        public WorkflowCustomInputDefinitionDto InputDefinition { get; set; }
        public WorkflowCustomDefinitionSettingDto SettingDefinition {get; set;}
    }
}
