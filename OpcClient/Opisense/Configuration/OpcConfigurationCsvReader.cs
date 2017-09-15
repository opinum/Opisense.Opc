using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;

namespace Opisense.OpcClient.Configuration
{
    public class OpcConfigurationCsvReader
    {
        private const int FastestReadCycle = 1;
        private readonly string configFileName;
        private readonly CsvConfiguration csvConfiguration;

        public OpcConfigurationCsvReader(string configFileName)
        {
            this.configFileName = configFileName;
            csvConfiguration = new CsvConfiguration();
            csvConfiguration.RegisterClassMap<CsvConfigurationRecordMapper>();
            csvConfiguration.IgnoreBlankLines = true;
            csvConfiguration.IgnoreHeaderWhiteSpace = true;
            csvConfiguration.IsHeaderCaseSensitive = false;
        }

        protected virtual TextReader GetReader()
        {
            return File.OpenText(configFileName);
        }

        protected virtual async Task<IList<CsvConfigurationRecord>> ReadCsv()
        {
            var records = new List<CsvConfigurationRecord>();
            await Task.Factory.StartNew(() =>
            {
                using (var textReader = GetReader())
                {
                    using (var csvReader = new CsvReader(textReader, csvConfiguration))
                    {
                        records = csvReader.GetRecords<CsvConfigurationRecord>().ToList();
                    }
                }
            });
            return records;
        }

        public async Task<IList<OpisenseOpcConfiguration>> Read(Func<string, Task> onError = null)
        {
            try
            {
                var result = new List<OpisenseOpcConfiguration>();
                var records = await ReadCsv();
                foreach (var recordsByOpcServer in records.GroupBy(r => r.OpcServer))
                {
                    var config = new OpisenseOpcConfiguration(recordsByOpcServer.Key.Trim());
                    foreach (var recordsByGroupName in recordsByOpcServer.GroupBy(r => r.GroupName))
                    {
                        var readCycle = TimeSpan.FromMinutes(Math.Max(recordsByGroupName.Min(r => r.ReadCycleMinutes), FastestReadCycle));
                        var group = new OpisenseOpcItemGroup(recordsByGroupName.Key.Trim(), readCycle);
                        var items = recordsByGroupName.Where(r => !string.IsNullOrWhiteSpace(r.TagName)).ToList();
                        if (items.Any())
                        {
                            config.OpisenseOpcItemGroups.Add(group);
                            group.OpisenseOpcItems.AddRange(items.Select(i => new OpisenseOpcItem {OpcItemName = i.TagName.Trim(), VariableId = i.VariableId}));
                        }
                    }
                    result.Add(config);
                }
                return result;

            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
                return new List<OpisenseOpcConfiguration>();
            }
        }
    }
}