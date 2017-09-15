using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Opisense.DataPusher;
using Opisense.OpcClient;
using Opisense.OpcClient.Configuration;

namespace Opisense.OpcWindowsService
{
    public class ServiceWorker : IServiceWorker
    {
        private readonly IOpisenseOpcConfigurationFactory opcConfigurationFactory;
        private readonly IOpisenseOpcConnectorFactory opcConnectorFactory;
        private readonly IDataPusher dataPusher;
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ServiceWorker));
        private readonly List<Task> opcTasks = new List<Task>();
        private Task mainTask;

        public ServiceWorker(IOpisenseOpcConfigurationFactory opcConfigurationFactory, IOpisenseOpcConnectorFactory opcConnectorFactory, IDataPusher dataPusher)
        {
            this.opcConfigurationFactory = opcConfigurationFactory;
            this.opcConnectorFactory = opcConnectorFactory;
            this.dataPusher = dataPusher;
        }

        protected virtual void PushData(Guid groupId, IList<OpisenseOpcItemValue> values)
        {
            foreach (var value in values)
            {
                Logger.Debug(value);
            }
            dataPusher.PushData(values.Select(v => new Data {Date = v.TimeStampUtc, Value = v.Value, VariableId = v.VariableId}).ToList(),
                FilterMode.DiscardNegativeId,
                error =>
                {
                    Logger.Error($"Error pushing data: {error}");
                    return Task.FromResult(0);
                });
        }

        public virtual Task Run(string configFile, string defaultOpcServer, CancellationToken cancellationToken)
        {
            var opcConfig = opcConfigurationFactory.ReadFromFile(configFile, defaultOpcServer, onError: error =>
            {
                Logger.Error($"Error reading OPC configuration file '{configFile}': {error}");
                return Task.FromResult(0);
            }).Result;

            DumpConfig(opcConfig);

            var connector = opcConnectorFactory.CreateOpisenseOpcConnector(OpcKind.OpcDa);

            mainTask = Task.Factory.StartNew(async () =>
            {
                string GetGroupName(Guid groupId)
                {
                    return opcConfig.SelectMany(c => c.OpisenseOpcItemGroups).Single(g => g.GroupId == groupId).GroupName;
                }

                var numberOfGroups = opcConfig.SelectMany(c => c.OpisenseOpcItemGroups).Count();
                if (numberOfGroups == 0)
                {
                    Logger.Warn("There is no OPC group to read");
                    return;
                }

                var readDistributionPeriod = TimeSpan.FromTicks(Math.Min(TimeSpan.FromSeconds(30).Ticks, TimeSpan.FromMinutes(5).Ticks / numberOfGroups));
                foreach (var config in opcConfig)
                {
                    foreach (var group in config.OpisenseOpcItemGroups)
                    {
                        opcTasks.Add(connector.PollGroup(cancellationToken,
                            config.OpcServerUrl,
                            group,
                            PushData,
                            (groupId, tagsCount) => Logger.Info($"Start reading {tagsCount} tag(s) from group '{GetGroupName(groupId)}'"),
                            (groupId, tagsCount, nextPollDate) => Logger.Info(
                                $"{tagsCount} tag(s) (having good quality) received from group '{GetGroupName(groupId)}', next polling time {nextPollDate:s} UTC"),
                            (groupId, error) => Logger.Error($"Error reading group '{GetGroupName(groupId)}': {error}")));
                        
                        //Avoid to start all group reads at the same time, distribute reads over time
                        await Task.Delay(readDistributionPeriod, cancellationToken);
                    }
                }
            }
            , cancellationToken
            , TaskCreationOptions.LongRunning
            , TaskScheduler.Default);

            return mainTask;
        }

        public virtual void WaitForEnd(CancellationToken cancellationToken)
        {
            Task.WaitAll(opcTasks.Union(new[] {mainTask}).ToArray(), cancellationToken);
        }

        public virtual void PushVariable(int pushVariableId, double pushValue)
        {
            PushData(Guid.NewGuid(), new List<OpisenseOpcItemValue>
            {
                new OpisenseOpcItemValue{OpcItemName = "Forced", TimeStampUtc = DateTime.UtcNow, VariableId = pushVariableId, GoodQuality = true, Value = pushValue}
            });
        }

        protected virtual void DumpConfig(IEnumerable<OpisenseOpcConfiguration> opcConfig)
        {
            foreach (var config in opcConfig)
            {
                foreach (var group in config.OpisenseOpcItemGroups)
                {
                    Logger.Info($"*** Server: '{config.OpcServerUrl}' ***");
                    Logger.Info($"***    Group: '{group.GroupName}', polling cycle {group.PollingCycle:g}");
                    foreach (var tag in group.OpisenseOpcItems)
                    {
                        Logger.Info($"***      Tag: '{tag.OpcItemName}' for variable Id {tag.VariableId}");
                    }
                }
            }
        }
    }
}