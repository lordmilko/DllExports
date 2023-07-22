#if NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DllExports.MSBuild
{
    public class RemoteHelper : MarshalByRefObject
    {
        private string[] files;

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs e)
        {
            var name = e.Name;

            if (e.Name != null && e.Name.Contains(","))
                name = name.Substring(0, name.IndexOf(','));

            foreach (var file in files)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileNameWithoutExtension(file), name))
                    return Assembly.LoadFile(file);
            }

            return null;
        }

        public override bool Equals(object obj)
        {
            if (obj is string)
            {
                files = Directory.EnumerateFiles(obj.ToString(), "*.dll").ToArray();

                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            }
            else
            {
                //For some reason when we use reflection to invoke the method in the remote AppDomain we're still ending up back
                //in our original domain; as such, we'll force ourselves to run in another domain by creating an object (RemoteHelper)
                //in the remote AppDomain and doing everything through here

                var arr = (object[])obj;

                Exporter.Export(
                    (bool) arr[0],
                    (string) arr[1],
                    (string) arr[2],
                    (string[]) arr[3],
                    (string) arr[4],
                    (bool) arr[5]
                );
            }

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override object InitializeLifetimeService() => null;
    }
}
#endif