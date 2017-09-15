using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Opisense.DataPusher
{
    public interface IDataPusher
    {
        Task PushData(IEnumerable<Data> data, FilterMode filterMode, Func<string, Task> onError = null);
    }
}