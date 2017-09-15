using CsvHelper.Configuration;

namespace Opisense.OpcClient.Configuration
{
    public sealed class CsvConfigurationRecord
    {
        public string OpcServer { get; set; }
        public int ReadCycleMinutes { get; set; }
        public string GroupName { get; set; }
        public string TagName { get; set; }
        public int VariableId { get; set; }
    }

    public sealed class CsvConfigurationRecordMapper : CsvClassMap<CsvConfigurationRecord>
    {
        //Spaces are considered part of a field and should not be ignored. (see RFC 4180), meaning that g1 will become "g1" (double quotes)
        // because double quotes are treated has normal since they do not start the field definition.
        //This is for us too restrictive, we trim double quotes from values
        public CsvConfigurationRecordMapper()
        {
            Map(m => m.OpcServer)
                .ConvertUsing(row =>
                {
                    var field = row.GetField<string>(nameof(CsvConfigurationRecord.OpcServer)).Trim().Trim('"');
                    return string.IsNullOrWhiteSpace(field) ? OpisenseOpcConfiguration.DefaultOpcServerUrl : field;
                });
            Map(m => m.ReadCycleMinutes)
                .ConvertUsing(row =>
                {
                    var field = row.GetField<string>(nameof(CsvConfigurationRecord.ReadCycleMinutes)).Trim().Trim('"');
                    return int.TryParse(field, out var parsedInt) ? parsedInt : OpisenseOpcItemGroup.DefaultPollingCycle.TotalMinutes;
                });
            Map(m => m.GroupName)
                .ConvertUsing(row =>
                {
                    var field = row.GetField<string>(nameof(CsvConfigurationRecord.GroupName)).Trim().Trim('"');
                    return string.IsNullOrWhiteSpace(field) ? OpisenseOpcConfiguration.DefaultOpcGroup : field;
                });
            Map(m => m.TagName)
                .ConvertUsing(row =>
                {
                    var field = row.GetField<string>(nameof(CsvConfigurationRecord.TagName)).Trim().Trim('"');
                    return field is null ? "" : field;
                });
            Map(m => m.VariableId)
                .ConvertUsing(row =>
                {
                    var field = row.GetField<string>(nameof(CsvConfigurationRecord.VariableId)).Trim().Trim('"');
                    return int.TryParse(field, out var parsedInt) ? parsedInt : -1;
                });
        }
    }
}
