using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Opisense.OpcClient.Configuration
{
    public interface IOpisenseOpcConfigurationFactory
    {
        Task<OpisenseOpcConfiguration> CreateFromJson(string json, bool validate = true, Func<string, Task> onError = null);
        Task<IList<OpisenseOpcConfiguration>> ReadFromFile(string fileName, string defaultOpcServerUrl, bool validate = true, Func<string, Task> onError = null);
        Task<bool> ValidateConfig(OpisenseOpcConfiguration config, Func<string, Task> onError);
    }
}