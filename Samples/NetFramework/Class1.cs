using System;
using System.Runtime.InteropServices;
using DllExports;

namespace NetFramework
{
    public class Class1
    {
        [DllExport("MyExport", CallingConvention.StdCall)]
        public static void InternalName()
        {
        }
    }
}