using System.IO;
using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif

namespace DllExports.MSBuild
{
    class IsolatedTaskRunner
    {
        public void Execute(ExportOptions options)
        {
#if NET
            var context = new NetCoreAssemblyLoadContext();
#else
            var context = new NetFrameworkAssemblyLoadContext();
#endif

            try
            {
                var assemblyPath = GetType().Assembly.Location;
                var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
                var dllExportsPath = Path.Combine(assemblyDirectory, "DllExports.dll");

                if (!File.Exists(dllExportsPath))
                    throw new FileNotFoundException($"Could not find '{dllExportsPath}'", dllExportsPath);

                context.SetDllDirectory(assemblyDirectory);

                using (var fileStream = File.OpenRead(dllExportsPath))
                {
                    try
                    {
                        var assembly = context.LoadAssembly(fileStream);

                        context.Export(assembly, options);
                    }
                    finally
                    {
                        //I'm not sure if its such a great idea to be closing the file AFTER we've already unloaded,
                        //so we kick off an unload here
                        context.Unload();
                    }
                }
            }
            finally
            {
                //If we crashed before we attempted to unload above, we need to unload here
                context.Unload();
            }
        }
    }

    interface IAssemblyLoadContext
    {
        Assembly LoadAssembly(Stream stream);

        void SetDllDirectory(string path);

        void Export(Assembly assembly, ExportOptions options);

        void Unload();
    }
}