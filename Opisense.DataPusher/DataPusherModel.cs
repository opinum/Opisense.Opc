using System;
using System.Collections.Generic;

namespace Opisense.DataPusher
{
   public class Data
    {
        public int VariableId { get; set; }
        public DateTime Date { get; set; }
        public double Value { get; set; }
    }

    public class StandardModel
    {
        public List<Data> Data { get; set; }
    }
}
