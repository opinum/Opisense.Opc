using System.Threading;
using System.Threading.Tasks;

namespace Opisense.OpcWindowsService
{
    public interface IServiceWorker
    {
        Task Run(string configFile, string defaultOpcServer, CancellationToken cancellationToken);
        void WaitForEnd(CancellationToken cancellationToken);
        void PushVariable(int pushVariableId, double pushValue);
    }
}