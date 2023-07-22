using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using dnlib.PE;
using CallingConvention = System.Runtime.InteropServices.CallingConvention;

[assembly: InternalsVisibleTo("DllExports.MSBuild")]
[assembly: InternalsVisibleTo("DllExports.Tests")]

namespace DllExports
{
    internal class Exporter
    {
        public static void Export(
            bool enabled,
            string inputFile,
            string outputFile,
            string[] architectures,
            string architectureNameFormat,
            bool removeInputFile)
        {
            var options = new ExportOptions
            {
                Enabled = enabled,
                InputFile = inputFile,
                OutputFile = outputFile,
                Architectures = architectures,
                ArchitectureNameFormat = architectureNameFormat,
                RemoveInputFile = removeInputFile
            };

            var moduleContext = ModuleDef.CreateModuleContext();

            //If we simply create a ModuleDefMD from a memory stream, we're not going to know our original filename
            //and be able to find our PDB. If we simply load the module from the filename, our file will be in use
            //if we attempt to overwrite it. As such, we tap into the underlying PEImage type, which lets us read the
            //entire byte array of the file AND specify the input filename
            var bytes = File.ReadAllBytes(options.InputFile);
            var peImage = new PEImage(bytes, options.InputFile);

            var module = ModuleDefMD.Load(peImage, moduleContext);

            var exportedMethods = module.Types.SelectMany(t => t.Methods)
                .Where(m => m.IsStatic)
                .Select(m => new
                {
                    Method = m,
                    Attrib = m.CustomAttributes.Find(typeof(DllExportAttribute).FullName)
                })
                .Where(v => v.Attrib != null)
                .ToArray();

            foreach (var item in exportedMethods)
            {
                var exportedMethod = item.Method;

                string exportName;
                string callingConvention = null;

                if (item.Attrib.ConstructorArguments.Count == 0)
                    exportName = exportedMethod.Name;
                else
                {
                    if (item.Attrib.ConstructorArguments.Count == 2)
                    {
                        var conv = (CallingConvention)item.Attrib.ConstructorArguments[1].Value;

                        switch (conv)
                        {
                            case CallingConvention.StdCall:
                                callingConvention = typeof(CallConvStdcall).Name;
                                break;

                            case CallingConvention.Cdecl:
                                callingConvention = typeof(CallConvCdecl).Name;
                                break;

                            case CallingConvention.FastCall:
                                callingConvention = typeof(CallConvFastcall).Name;
                                break;

                            case CallingConvention.ThisCall:
                                callingConvention = typeof(CallConvThiscall).Name;
                                break;

                            case CallingConvention.Winapi:
                                callingConvention = typeof(CallConvStdcall).Name;
                                break;

                            default:
                                throw new NotSupportedException($"Calling convention {conv} is not supported");
                        }
                    }

                    exportName = item.Attrib.ConstructorArguments[0].Value?.ToString();
                }

                if (callingConvention == null)
                    callingConvention = typeof(CallConvStdcall).Name;

                exportedMethod.ExportInfo = new MethodExportInfo(exportName);
                exportedMethod.IsUnmanagedExport = true;

                exportedMethod.MethodSig.RetType = new CModOptSig(
                    module.CorLibTypes.GetTypeRef("System.Runtime.CompilerServices", callingConvention),
                    exportedMethod.MethodSig.RetType
                );

                exportedMethod.CustomAttributes.Remove(item.Attrib);
            }

            module.IsILOnly = false;

            ClearEditAndContinue(module);

            var outputFiles = options.CalculateOutputFiles();

            foreach (var output in outputFiles)
            {
                var moduleOptions = GetModuleOptions(module, output.Is32Bit);

                var dir = Path.GetDirectoryName(output.Path);

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                moduleOptions.WritePdb = true;

                module.Write(options.OutputFile, moduleOptions);
            }
        }

        private static ModuleWriterOptions GetModuleOptions(ModuleDefMD module, bool? is32Bit)
        {
            var moduleOptions = new ModuleWriterOptions(module);

            moduleOptions.Cor20HeaderOptions.Flags &= ~(ComImageFlags.ILOnly);

            if (is32Bit != null)
            {
                moduleOptions.PEHeadersOptions.Machine = is32Bit.Value ? Machine.I386 : Machine.AMD64;

                if (is32Bit.Value)
                {
                    moduleOptions.Cor20HeaderOptions.Flags |= ComImageFlags.Bit32Required;
                    moduleOptions.Cor20HeaderOptions.Flags &= ~(ComImageFlags.Bit32Preferred);
                }
            }

            return moduleOptions;
        }

        private static void ClearEditAndContinue(ModuleDefMD module)
        {
            var debuggableAttrib = module.Assembly.CustomAttributes.Find("System.Diagnostics.DebuggableAttribute");

            if (debuggableAttrib != null && debuggableAttrib.ConstructorArguments.Count == 1)
            {
                var arg = debuggableAttrib.ConstructorArguments[0];

                // VS' debugger crashes if value == 0x107, so clear EnC bit
                if (arg.Type.FullName == "System.Diagnostics.DebuggableAttribute/DebuggingModes" && arg.Value is int value && value == 0x107)
                {
                    arg.Value = value & ~(int)DebuggableAttribute.DebuggingModes.EnableEditAndContinue;
                    debuggableAttrib.ConstructorArguments[0] = arg;
                }
            }
        }
    }
}
