using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;
using log4net;

namespace Opisense.DataPusher
{
    public enum FilterMode
    {
        KeepNegativeId,
        DiscardNegativeId
    }

    public class Pusher : IDataPusher
    {
        private static readonly string[] DataPusherScopes = { "openid", "push-data" };

        private static readonly string OpisenseUserName = ConfigurationManager.AppSettings.Get("OpisenseUserName");
        private static readonly string OpisenseUserPassword = ConfigurationManager.AppSettings.Get("OpisenseUserPassword");
        private static readonly string ClientApiKey = ConfigurationManager.AppSettings.Get("ClientApiKey");
        private static readonly string ClientSecretKey = ConfigurationManager.AppSettings.Get("ClientSecretKey");
        private static readonly string IdentityTokenUrl = ConfigurationManager.AppSettings.Get("IdentityTokenUrl");
        private static readonly string PushUrl = ConfigurationManager.AppSettings.Get("PushUrl");
        private bool firstRun = true;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(Pusher));

        protected async Task<string> GetBearerToken()
        {
            var response = await new TokenClient(IdentityTokenUrl, ClientApiKey, ClientSecretKey)
                .RequestResourceOwnerPasswordAsync(OpisenseUserName, OpisenseUserPassword, string.Join(" ", DataPusherScopes));

            if (response.IsError)
            {
                throw new Exception(response.Error);
            }
            return response.AccessToken;
        }

        protected async Task<HttpClient> GetAuthenticatedClient()
        {
            var bearerToken = await GetBearerToken();
            var client = new HttpClient();
            client.SetBearerToken(bearerToken);
            return client;
        }

        protected void DumpConfig()
        {
            string CheckConfigItem(string configItemValue, bool secret)
            {
                if (string.IsNullOrWhiteSpace(configItemValue))
                    return "Empty";
                if (configItemValue.IndexOf("NOT SET", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    return "Not set, should be set by SecretConfigFiles/OpcAppSettingsSecrets.config";
                if (secret)
                    return "Set but secret";

                return $"Set to {configItemValue}";
            }

            if (!Logger.IsInfoEnabled)
                return;

            Logger.Info("Data pusher config check:");
            Logger.Info($"OpisenseUserName :{CheckConfigItem(OpisenseUserName, true)}");
            Logger.Info($"OpisenseUserPassword :{CheckConfigItem(OpisenseUserPassword, true)}");
            Logger.Info($"ClientApiKey :{CheckConfigItem(ClientApiKey, true)}");
            Logger.Info($"ClientSecretKey :{CheckConfigItem(ClientSecretKey, true)}");
            Logger.Info($"IdentityTokenUrl :{CheckConfigItem(IdentityTokenUrl, false)}");
            Logger.Info($"PushUrl :{CheckConfigItem(PushUrl, false)}");
        }

        public async Task PushData(IEnumerable<Data> data, FilterMode filterMode, Func<string, Task> onError = null)
        {
            if (firstRun)
            {
                DumpConfig();
                firstRun = false;
            }

            var reducedData = data.ToList();
            if (filterMode == FilterMode.DiscardNegativeId)
            {
                reducedData = reducedData.Where(d => d.VariableId >= 0).ToList();
            }

            if (!reducedData.Any())
                return;

            var model = new StandardModel {Data = reducedData};
            try
            {
                using (var httpClient = await GetAuthenticatedClient())
                {
                    using (var response = await httpClient.PostAsJsonAsync(PushUrl, model))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            if (!(onError is null))
                            {
                                await onError($"Error pushing data: HTTP code {response.StatusCode} - {response.ReasonPhrase}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (onError is null)
                    throw;
                await onError($"Error pushing data: {ex.Message}");
            }
        }

    }
}
