using System;

namespace IdentityManager2.Configuration
{
    public class IdentityManagerOptions
    {
        public SecurityConfiguration SecurityConfiguration { get; set; } = new LocalhostSecurityConfiguration();

        public string RootPathBase { get; set; } = "";

        internal void Validate()
        {
            if (SecurityConfiguration == null)
            {
                throw new Exception("SecurityConfiguration is required.");
            }

            SecurityConfiguration.Validate();
        }
    }
}