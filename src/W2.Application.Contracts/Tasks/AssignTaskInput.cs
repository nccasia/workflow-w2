using System;
using System.Collections.Generic;
using System.Text;
using W2.Tasks;

namespace W2.Tasks
{
    public class AssignTaskInput
    {
        public Guid UserId { get; set; }
        public string WorkflowInstanceId { get; set; }
        public string ApproveSignal { get; set; }
        public string RejectSignal { get; set; }
        public string TaskTitle { get; set; }
        public string DynamicActionData { get; set; }
        public string Description { get; set; }
        public List<string> EmailTo { get; set; }
        public List<string> OtherActionSignals { get; set; } = new List<string>();
    }
}
