using System;

namespace Opisense.OpcClient
{
    public enum OpcKind
    {
        OpcDa,
        OpcUa,
    }

    public class OpisenseOpcConnectorFactory : IOpisenseOpcConnectorFactory
    {
        public IOpisenseOpcConnector CreateOpisenseOpcConnector(OpcKind opcKind)
        {
            switch (opcKind)
            {
                case OpcKind.OpcDa:
                    return new OpisenseOpcDaConnector();
                case OpcKind.OpcUa:
                    throw new ArgumentOutOfRangeException(nameof(opcKind), opcKind, "OPC UA is not yet implemented"); ;
                default:
                    throw new ArgumentOutOfRangeException(nameof(opcKind), opcKind, null);
            }
        }
    }
}
