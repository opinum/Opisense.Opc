using System;
using System.Collections.Generic;

namespace Opisense.OpcClient
{
    public class OpisenseOpcConfiguration
    {
        public static string DefaultOpcServerUrl = "Default";
        public static string DefaultOpcGroup = "DefaultGroup";
        public string OpcServerUrl { get; set; }
        public List<OpisenseOpcItemGroup> OpisenseOpcItemGroups { get; set; } = new List<OpisenseOpcItemGroup>();

        public OpisenseOpcConfiguration() : this(DefaultOpcServerUrl)
        {
        }

        public OpisenseOpcConfiguration(string opcServerUrl)
        {
            OpcServerUrl = opcServerUrl;
        }
    }

    public class OpisenseOpcItemGroup
    {
        public Guid GroupId { get; } = Guid.NewGuid();
        public static TimeSpan DefaultPollingCycle = TimeSpan.FromMinutes(15);
        public string GroupName { get; set; }
        public TimeSpan PollingCycle { get; set; }
        public List<OpisenseOpcItem> OpisenseOpcItems { get; set; } = new List<OpisenseOpcItem>();

        public OpisenseOpcItemGroup() : this(Guid.NewGuid().ToString(), DefaultPollingCycle)
        {
        }

        public OpisenseOpcItemGroup(string groupName, TimeSpan pollingCycle)
        {
            GroupName = groupName;
            PollingCycle = pollingCycle;
        }
    }
}

