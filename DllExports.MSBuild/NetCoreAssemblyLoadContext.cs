#if NET
using System;
using System.Linq;
using System.Runtime.Loader;
using System.IO;
using System.Reflection;

namespace DllExports.MSBuild
{
    class AssemblyContext : AssemblyLoadContext
    {
        public AssemblyContext() : base(true)
        {
        }
    }

    class NetCoreAssemblyLoadContext : IAssemblyLoadContext
    {
        private AssemblyContext loader = new AssemblyContext();
        private bool unloaded;

        public NetCoreAssemblyLoadContext()
        {
        }

        public Assembly LoadAssembly(Stream stream) =>
            loader.LoadFromStream(stream);

        public void SetDllDirectory(string path)
        {
            var files = Directory.EnumerateFiles(path, "*.dll").ToArray();

            loader.Resolving += (ctx, name) =>
            {
                foreach (var file in files)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileNameWithoutExtension(file), name.Name))
                        return ctx.LoadFromAssemblyPath(file);
                }
                
                return null;
            };
        }

        public void Export(Assembly assembly, ExportOptions options)
        {
            var exporterType = assembly.GetType("DllExports.Exporter");
            var exportMethod = exporterType.GetMethod("Export");

            exportMethod.Invoke(null, new object[]
            {
                options.Enabled,
                options.InputFile,
                options.OutputFile,
                options.Architectures,
                options.ArchitectureNameFormat,
                options.RemoveInputFile
            });
        }

        public void Unload()
        {
            if (unloaded)
                return;

            loader.Unload();
            unloaded = true;
        }
    }
}
#endif