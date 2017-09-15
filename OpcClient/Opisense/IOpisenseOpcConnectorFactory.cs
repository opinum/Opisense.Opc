namespace Opisense.OpcClient
{
    public interface IOpisenseOpcConnectorFactory
    {
        IOpisenseOpcConnector CreateOpisenseOpcConnector(OpcKind opcKind);
    }
}