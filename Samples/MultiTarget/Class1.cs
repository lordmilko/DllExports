using System;
using System.Runtime.InteropServices;
using DllExports;

namespace MultiTarget
{
    public class Class1
    {
        [DllExport("MyExport")]
        public static void InternalName()
        {
        }
    }
}