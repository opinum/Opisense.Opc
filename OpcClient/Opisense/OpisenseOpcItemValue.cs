using System;

namespace Opisense.OpcClient
{
    public class OpisenseOpcItemValue : OpisenseOpcItem
    {
        public DateTime TimeStampUtc { get; set; }
        public double Value { get; set; }
        public bool GoodQuality { get; set; }

        public override string ToString()
        {
            return $"Tag<{OpcItemName}>, VariableId<{VariableId}>, TimeStampUtc<{TimeStampUtc:s}>, Value<{Value}>, Good<{GoodQuality}>";
        }
    }
}