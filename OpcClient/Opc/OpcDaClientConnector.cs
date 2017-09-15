using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Opc;
using Opc.Da;

namespace Opisense.OpcClient
{
    public enum OpcPropertyId
    {
        CanonicalDataType = 1,
        Value = 2,
        Quality = 3,
        TimeStamp = 4,
        AccessRights = 5,
        Units = 100,
        Description = 101,
    }

    public class OpcDaClientConnector
    {
        //// TODO: implement write
        //Opc.Da.ItemValue[] writeValues = new Opc.Da.ItemValue[3];
        //writeValues[0] = new Opc.Da.ItemValue();
        //writeValues[1] = new Opc.Da.ItemValue();
        //writeValues[2] = new Opc.Da.ItemValue();
        //writeValues[0].ServerHandle = group.Items[0].ServerHandle;
        //writeValues[0].Value = 0;
        //writeValues[1].ServerHandle = group.Items[1].ServerHandle;
        //writeValues[1].Value = 0;
        //writeValues[2].ServerHandle = group.Items[2].ServerHandle;
        //writeValues[2].Value = 0;
        //Opc.IRequest req;
        //group.Write(writeValues, 321, WriteCompleteCallback, out req);

        //// and now read the items again
        //group.Read(group.Items, 123, ReadCompleteCallback, out req);


        private OpcDaClientConnector()
        {
        }

        public static OpcDaClientConnector WithNewInstance()
        {
            return new OpcDaClientConnector();
        }

        private void ThrowOrReport(Exception ex, Action<Exception> reportException)
        {
            if (reportException == null || ex is OperationCanceledException) throw ex;
            reportException(ex);
        }

        public async Task ReadItems(CancellationToken cancellationToken, string opcServerUrl, IEnumerable<OpisenseOpcItem> opisenseOpcItems, Func<ItemValue, Task> onGoodItem, Func<ItemValue, Task> onBadItem = null, Action<Exception> onError = null)
        {
            try
            {
                await Task.Run(() =>
                {
                    WithConnectedOpcDaServer(opcServerUrl, (serverConnector, server) =>
                    {
                        serverConnector.WithGroupOfItems(server, Guid.NewGuid().ToString(),
                            opisenseOpcItems,
                            (groupConnector, subscription) =>
                            {
                                groupConnector.WithGroupRefreshResult(subscription,
                                    async itemValueResults =>
                                    {
                                        foreach (var itemValueResult in itemValueResults)
                                        {
                                            if (itemValueResult.ResultID != ResultID.S_OK ||
                                                (itemValueResult.QualitySpecified &&
                                                    itemValueResult.Quality != Quality.Good))
                                            {
                                                if (onBadItem != null) await onBadItem(itemValueResult);
                                            }
                                            else
                                                await onGoodItem(itemValueResult);
                                        }
                                    },
                                    ex => ThrowOrReport(new Exception($"Exception reading items on OPC server<{opcServerUrl}>", ex), onError));
                            }, ex => ThrowOrReport(new Exception($"Exception creating items group on OPC server<{opcServerUrl}>", ex), onError));
                    }, ex => ThrowOrReport(new Exception($"Exception connecting OPC server<{opcServerUrl}>", ex), onError));
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void WithConnectedOpcDaServer(string opcServerUrl, Action<OpcDaClientConnector, Opc.Da.Server> action, Action<Exception> onError = null)
        {
            var url = new URL(opcServerUrl);
            using (var server = new Opc.Da.Server(new OpcCom.Factory(), null))
            {
                var connectData = new ConnectData(new System.Net.NetworkCredential());
                server.Connect(url, connectData);
                action(this, server);
            }
        }

        public void WithGroupOfItems(Opc.Da.Server server, string groupName, IEnumerable<OpisenseOpcItem> opcItems, Action<OpcDaClientConnector, Subscription> action, Action<Exception> onError = null)
        {
            if (opcItems is null)
                return;

            var opcItemsList = opcItems.ToList();
            if (!opcItemsList.Any())
                return;

            var groupState = new SubscriptionState
            {
                Name = groupName,
                Active = false
            };

            using (var opcGroup = server.CreateSubscription(groupState))
            {
                //There is a bug in the library, ClientHandle is not propagated with item results
                var subscriptionItems = opcItemsList.Select(i => new Item { ItemName = i.OpcItemName, ClientHandle = i.VariableId});
                opcGroup.AddItems(subscriptionItems.ToArray());
                action(this, opcGroup as Subscription);
            }
        }

        public void WithGroupRefreshResult(Subscription opcGroup, Action<ItemValueResult[]> action, Action<Exception> onError = null)
        {
            var results = opcGroup.Read(opcGroup.Items);
            action(results);
        }

        private static readonly PropertyID[] PropertyIds =
        {
            new PropertyID((int)OpcPropertyId.CanonicalDataType),
            new PropertyID((int)OpcPropertyId.Quality),
        };

        public void WithItemProperties(Opc.Da.Server server, string itemName, Action<ItemPropertyCollection> action, Action<Exception> onError = null)
        {
            try
            {
                var prop = server.GetProperties(new[] {new ItemIdentifier(itemName)}, PropertyIds, false).Single();
                action(prop);
            }
            catch (InvalidOperationException)
            {
                throw new Exception($"Item name<{itemName}> does not exist on server<{server.Url}>");
            }
        }

        public async Task<Dictionary<string, IReadOnlyCollection<ItemProperty>>> BrowseAllItems(CancellationToken cancellationToken, string opcServerUrl, Action<Exception> onError = null)
        {
            var result = new Dictionary<string, IReadOnlyCollection<ItemProperty>>();

            void BrowseItems(BrowseElement node, Opc.Da.IServer server)
            {
                var itemIdentifier = node == null ? null : new ItemIdentifier(node.ItemName);
                if (node != null && node.IsItem)
                {
                    var properties = server.GetProperties(new[] {new ItemIdentifier(node.ItemName)}, PropertyIds, false);
                    result.Add(node.ItemName, properties.First().Cast<ItemProperty>().ToList());
                }
                if (node == null || node.HasChildren)
                {
                    foreach (var childNode in server.Browse(itemIdentifier, new BrowseFilters {BrowseFilter = browseFilter.all}, out var _))
                    {
                        BrowseItems(childNode, server);
                    }
                }
            }

            try
            {
                await Task.Run(() =>
                    {
                        try
                        {
                            WithNewInstance()
                                .WithConnectedOpcDaServer(opcServerUrl,
                                    (_, server) =>
                                    {
                                        BrowseItems(null, server);
                                    },
                                    onError);
                        }
                        catch (Exception ex)
                        {
                            ThrowOrReport(ex, onError);
                        }
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                //We don't want partial result upon cancellation
                return new Dictionary<string, IReadOnlyCollection<ItemProperty>>();
            }
            return result;
        }
    }
}