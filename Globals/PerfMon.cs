using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerilogLanguage
{
    public static partial class VerilogGlobals
    {

        public static class PerfMon
        {
            public static int VerilogTokenTag_Count = 0;
            public static int VerilogTokenTagger_Count = 0;
            public static int CommandFilter_QueryStatus_Count = 0;

            private static void init()
            {
            }
        }

    }

}
