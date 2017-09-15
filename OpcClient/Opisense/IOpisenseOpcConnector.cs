using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Opisense.OpcClient
{
    public interface IOpisenseOpcConnector
    {
        Task<IEnumerable<OpisenseOpcItemValue>> ReadGroup(
            CancellationToken cancellationToken, 
            string opcServerUrl,
            OpisenseOpcItemGroup opcItemGroup, 
            Action<IEnumerable<string>> onError = null, 
            bool discardBadValues = true);

        Task PollGroup(
            CancellationToken cancellationToken,
            string opcServerUrl,
            OpisenseOpcItemGroup opcItemGroup,
            Action<Guid, IList<OpisenseOpcItemValue>> onGroupReadResult,
            Action<Guid, int> beforeGroupRead = null,
            Action<Guid, int, DateTime> afterGroupRead = null,
            Action<Guid, string> onGroupError = null);

        Task<Dictionary<string, IEnumerable<OpisenseOpcItemProperty>>> BrowseAllItems(
            CancellationToken cancellationToken, 
            string opcServerUrl, 
            Action<string> onError = null);

    }
}