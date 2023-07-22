#if !NET
using System;
using System.IO;
using System.Reflection;

namespace DllExports.MSBuild
{
    internal class NetFrameworkAssemblyLoadContext : MarshalByRefObject, IAssemblyLoadContext
    {
        private AppDomain appDomain;
#pragma warning disable 649
        private object helper;
#pragma warning restore 649
        private bool unloaded;

        public NetFrameworkAssemblyLoadContext()
        {
            appDomain = AppDomain.CreateDomain("DllExportAppDomain");

#if NETFRAMEWORK
            helper = appDomain.CreateInstanceFromAndUnwrap(
                GetType().Assembly.Location,
                typeof(RemoteHelper).FullName,
                false,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[0],
                null,
                null
            );
#else
            throw new NotSupportedException();
#endif
        }

        public Assembly LoadAssembly(Stream stream)
        {
            //Attempting to load DllExports.dll inside of MSBuild will cause it to try and look the DLL up
            //in the Visual Studio installation directory. Our assembly resolver is never called. As such,
            //we don't even bother trying to pre-emptively load DllExports.dll. We don't need to anyway,
            //since it will "just work" when we dispatch to it via RemoteHelper
            return null;
        }

        public void SetDllDirectory(string path)
        {
            //I don't know why MSBuild gets upset when I try and cast my transparent proxy to RemoteHelper,
            //and I don't really care. So we'll use type object instead and abuse the Equals method to dispatch
            //everything
            helper.Equals(path);
        }

        public void Export(Assembly assembly, ExportOptions options)
        {
            //I don't know why MSBuild gets upset when I try and cast my transparent proxy to RemoteHelper,
            //and I don't really care. So we'll use type object instead and abuse the Equals method to dispatch
            //everything
            helper.Equals(
                new object[]
                {
                    options.Enabled,
                    options.InputFile,
                    options.OutputFile,
                    options.Architectures,
                    options.ArchitectureNameFormat,
                    options.RemoveInputFile
                }
            );
        }

        public void Unload()
        {
            if (unloaded)
                return;

            AppDomain.Unload(appDomain);
            unloaded = true;
        }
    }
}
#endif