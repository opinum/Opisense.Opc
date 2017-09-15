using Opc;

namespace Opisense.OpcClient
{
    public class OpisenseOpcItemProperty
    {
        public string PropertyName { get; set; }
        public string Description { get; set; }
        public System.Type DataType { get; set; }
        public object Value { get; set; }
        public string ItemName { get; set; }
        public string ItemPath { get; set; }
        public ResultID ResultId { get; set; }
        public string DiagnosticInfo { get; set; }
    }
}
