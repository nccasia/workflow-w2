using System;
using System.Collections.Generic;
using System.Text;

namespace W2.Login
{
    public class AuthDto
    {
        public string userNameOrEmailAddress { get; set; }
        public string password { get; set; }
        public bool? rememberClient { get; set; }
    }

    public class AuthUser
    {
        public string Token { get; set; } = string.Empty;
        public int AccessFailedCount { get; set; }
    }
}
