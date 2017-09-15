using Ninject;
using Ninject.Modules;
using Opisense.DataPusher;
using Opisense.OpcClient;
using Opisense.OpcClient.Configuration;

namespace Opisense.OpcWindowsService
{
    internal class ServiceNinjectModule : NinjectModule
    {
        public override void Load()
        {
            Kernel?.Load<OpcClientNinjectModule>();
            Kernel?.Bind<IOpisenseOpcConfigurationFactory>().To<OpisenseOpcConfigurationFactory>();
            Kernel?.Bind<IOpisenseOpcConnectorFactory>().To<OpisenseOpcConnectorFactory>();
            Kernel?.Bind<IDataPusher>().To<Pusher>();
            Kernel?.Bind<IServiceWorker>().To<ServiceWorker>();
        }
    }
}