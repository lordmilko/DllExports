using System;
using System.Runtime.InteropServices;
using DllExports;

namespace SingleTarget
{
    public class Class1
    {
        [DllExport("MyExport", CallingConvention.Cdecl)]
        public static void InternalName()
        {
        }
    }
}
