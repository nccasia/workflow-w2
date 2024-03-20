using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using W2.WorkflowDefinitions;

namespace W2.Web.Pages.WorkflowDefinitions.Models
{
    public class WorkflowCustomInputPropertyDefinitionViewModel
    {
        [Required]
        [DisplayName("Property Name")]
        public string Name { get; set; }

        [DisplayName("Property Type")]
        public WorkflowInputDefinitionProperyType Type { get; set; }

        [DisplayName("Required")]
        public bool IsRequired { get; set; }

        [DisplayName("IsTitle")]
        public bool IsTitle { get; set; }
    }
}
