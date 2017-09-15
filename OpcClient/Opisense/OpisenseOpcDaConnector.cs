using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Opc.Da;

namespace Opisense.OpcClient
{
    public class OpisenseOpcDaConnector : IOpisenseOpcConnector
    {
        private static readonly ILog Logger = LogManager.GetLogger(nameof(OpisenseOpcDaConnector));

        public Task PollGroup(CancellationToken cancellationToken, string opcServerUrl, OpisenseOpcItemGroup opcItemGroup, Action<Guid, IList<OpisenseOpcItemValue>> onGroupReadResult, Action<Guid,int> beforeGroupRead = null, Action<Guid, int, DateTime> afterGroupRead = null, Action<Guid, string> onGroupError = null)
        {
            return Task.Factory.StartNew(async () =>
            {
                void HandleErrors(IEnumerable<string> errors)
                {
                    foreach (var error in errors)
                    {
                        onGroupError?.Invoke(opcItemGroup.GroupId, error);
                    }
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        beforeGroupRead?.Invoke(opcItemGroup.GroupId, opcItemGroup.OpisenseOpcItems.Count);

                        var res = (await ReadGroup(cancellationToken, 
                            opcServerUrl, 
                            opcItemGroup, 
                            HandleErrors)).ToList();

                        afterGroupRead?.Invoke(opcItemGroup.GroupId, res.Count, DateTime.UtcNow.Add(opcItemGroup.PollingCycle));
                        onGroupReadResult(opcItemGroup.GroupId, res);
                    }
                    catch (Exception)
                    {
                        //Stay alive even if the calee fails
                    }

                    try
                    {
                        await Task.Delay(opcItemGroup.PollingCycle, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
            , cancellationToken
            , TaskCreationOptions.LongRunning
            , TaskScheduler.Default);
        }

        public async Task<IEnumerable<OpisenseOpcItemValue>> ReadGroup(CancellationToken cancellationToken, string opcServerUrl, OpisenseOpcItemGroup opcItemGroup, Action<IEnumerable<string>> onError = null, bool discardBadValues = true)
        {
            var opisenseOpcItemsList =  opcItemGroup.OpisenseOpcItems.ToList();
            var readResult = new List<OpisenseOpcItemValue>(opisenseOpcItemsList.Count);
            var errors = new List<string>();

            void HandleItemValue(ItemValue itemValue)
            {
                //There is a bug in the library, ClientHandle is not propagated with item results
                //if (!int.TryParse(itemValue.ClientHandle.ToString(), out int variableId))
                {
                }

                try
                {
                    var variableIds = opisenseOpcItemsList.Where(i => i.OpcItemName.Equals(itemValue.ItemName, StringComparison.InvariantCultureIgnoreCase)).Select(i => i.VariableId);
                    readResult.AddRange(variableIds.Select(variableId => new OpisenseOpcItemValue
                    {
                        TimeStampUtc = new DateTime(itemValue.Timestamp.Ticks, DateTimeKind.Local).ToUniversalTime(),
                        OpcItemName = itemValue.ItemName,
                        VariableId = variableId,
                        Value = ResultAsDouble(itemValue),
                        GoodQuality = itemValue.Quality == Quality.Good
                    }));
                }
                catch (Exception ex)
                {
                    var message = $"Exception handling item '{itemValue.ItemName}' with value '{itemValue.Value}': {ex.Message}";
                    errors.Add(message);
                }
            }


            await OpcDaClientConnector
                .WithNewInstance()
                .ReadItems(cancellationToken, opcServerUrl, opisenseOpcItemsList,
                    goodValue =>
                    {
                        HandleItemValue(goodValue);
                        return Task.FromResult(0);
                    },
                    badValue =>
                    {
                        if(!discardBadValues)
                            HandleItemValue(badValue);
                        return Task.FromResult(0);
                    },
                    exception => errors.Add(exception.Message));
            if (errors.Any())
            {
                if (onError is null)
                {
                    Logger.Error($"Exception reading items from OPC server {opcServerUrl}: {string.Join(", ", errors)}");
                }
                else
                {
                    onError(errors);
                }
            }
            return readResult;
        }

        private static double ResultAsDouble(ItemValue itemValue)
        {
            return Convert.ToDouble(itemValue.Value);
        }

        private void ThrowOrReport(Exception ex, Action<string> reportException)
        {
            if (reportException == null || ex is OperationCanceledException) throw ex;
            reportException(ex.Message);
        }

        public async Task<Dictionary<string, IEnumerable<OpisenseOpcItemProperty>>> BrowseAllItems(CancellationToken cancellationToken,
            string opcServerUrl,
            Action<string> onError = null)
        {
            OpisenseOpcItemProperty ConvertProperty(ItemProperty property)
            {
                return new OpisenseOpcItemProperty
                {
                    PropertyName  = property.ID.Name.Name,
                    Description = property.Description,
                    DataType = property.DataType,
                    Value = property.Value,
                    ItemName = property.ItemName,
                    ItemPath = property.ItemPath,
                    ResultId = property.ResultID,
                    DiagnosticInfo = property.DiagnosticInfo
                };
            }
            var exisitingTags = await OpcDaClientConnector.WithNewInstance().BrowseAllItems(cancellationToken, opcServerUrl, exception => ThrowOrReport(exception, onError));
            var result = exisitingTags.ToDictionary(
                keySelector => keySelector.Key,
                valueSelector => valueSelector.Value.Select(ConvertProperty));

            return result;
        }
    }
}