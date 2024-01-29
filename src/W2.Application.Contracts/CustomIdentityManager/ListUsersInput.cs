using System;
using System.Collections.Generic;
using System.Text;
using W2.Tasks;

namespace W2.CustomIdentityManager
{
    public class ListUsersInput
    {
        public int MaxResultCount { get; set; }
        public int SkipCount { get; set; }
        public string Filter { get; set; }
        public string Roles { get; set; }
        public string Sorting { get; set; }
    }
}
