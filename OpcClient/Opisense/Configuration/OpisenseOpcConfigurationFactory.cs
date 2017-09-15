using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Opisense.OpcClient.Configuration
{
    public class OpisenseOpcConfigurationFactory : IOpisenseOpcConfigurationFactory
    {
        public static OpisenseOpcConfiguration EmptyConfiguration = new OpisenseOpcConfiguration();

        protected virtual async Task ThrowOrReport(Exception ex, Func<string, Task> reportException)
        {
            if (reportException == null) throw ex;
            await reportException(ex.Message);
        }

        public async Task<IList<OpisenseOpcConfiguration>> ReadFromFile(string fileName, string defaultOpcServerUrl, bool validate = true, Func<string, Task> onError = null)
        {
            try
            {
                var configs = await new OpcConfigurationCsvReader(fileName).Read(onError);
                if (validate)
                {
                    foreach(var config in configs)
                        await ValidateConfig(config, onError);
                }

                foreach (var config in configs)
                {
                    if (config.OpcServerUrl.Equals(OpisenseOpcConfiguration.DefaultOpcServerUrl, StringComparison.InvariantCultureIgnoreCase))
                    {
                        config.OpcServerUrl = defaultOpcServerUrl;
                    }
                }

                return configs;
            }
            catch (Exception ex)
            {
                await ThrowOrReport(ex, onError);
                return new List<OpisenseOpcConfiguration>{EmptyConfiguration};
            }
        }

        public async Task<OpisenseOpcConfiguration> CreateFromJson(string json, bool validate = true, Func<string, Task> onError = null)
        {
            try
            {
                var config = JsonConvert.DeserializeObject<OpisenseOpcConfiguration>(json);
                if (validate)
                {
                    await ValidateConfig(config, onError);
                }

                return config;
            }
            catch (Exception ex)
            {
                await ThrowOrReport(ex, onError);
                return EmptyConfiguration;
            }
        }

        public async Task<bool> ValidateConfig(OpisenseOpcConfiguration config, Func<string, Task> onError)
        {
            if (config is  null)
            {
                await ThrowOrReport(new Exception("The configuration is null"), onError);
                return false;
            }

            if (string.IsNullOrEmpty(config.OpcServerUrl))
            {
                await ThrowOrReport(new Exception("The server is null"), onError);
                return false;
            }

            if (config.OpisenseOpcItemGroups == null || !config.OpisenseOpcItemGroups.Any())
            {
                await ThrowOrReport(new Exception("There is no group to read"), onError);
                return false;
            }

            if (config.OpisenseOpcItemGroups.Any(g => string.IsNullOrWhiteSpace(g.GroupName)))
            {
                await ThrowOrReport(new Exception("At least one group name is empty"), onError);
                return false;
            }

            foreach(var invalidPollingCycle in config.OpisenseOpcItemGroups.Where(g => g.PollingCycle <= TimeSpan.Zero ))
            {
                await ThrowOrReport(new Exception($"The polling cycle {invalidPollingCycle.PollingCycle:g} of group '{invalidPollingCycle.GroupName}' is invalid (<= 0)"), onError);
                return false;
            }

            foreach (var configOpisenseOpcItemGroup in config.OpisenseOpcItemGroups)
            {
                if (configOpisenseOpcItemGroup.OpisenseOpcItems.Any(i => string.IsNullOrWhiteSpace(i.OpcItemName)))
                {
                    await ThrowOrReport(new Exception($"At least one item name is empty in group '{configOpisenseOpcItemGroup.GroupName}'"), onError);
                    return false;
                }
            }

            return true;
        }
    }
}

