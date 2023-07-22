using System;
using System.Runtime.InteropServices;

namespace DllExports
{
    [AttributeUsage(AttributeTargets.Method)]
    public class DllExportAttribute : Attribute
    {
        public string ExportName { get; }

        public CallingConvention CallingConvention { get; }

        public DllExportAttribute()
        {
            CallingConvention = CallingConvention.StdCall;
        }

        public DllExportAttribute(string exportName)
        {
            ExportName = exportName;
            CallingConvention = CallingConvention.StdCall;
        }
        
        public DllExportAttribute(string exportName, CallingConvention callingConvention)
        {
            ExportName = exportName;
            CallingConvention = callingConvention;
        }
    }
}