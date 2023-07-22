using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace DllExports.MSBuild
{
    public class GenerateDllExports : Task
    {
        private ExportOptions options = new ExportOptions();

        public bool Enabled
        {
            get => options.Enabled;
            set => options.Enabled = value;
        }

        public string InputFile
        {
            get => options.InputFile;
            set => options.InputFile = value;
        }

        public string OutputFile
        {
            get => options.OutputFile;
            set => options.OutputFile = value;
        }

        public string[] Architectures
        {
            get => options.Architectures;
            set => options.Architectures = value;
        }

        public string ArchitectureNameFormat
        {
            get => options.ArchitectureNameFormat;
            set => options.ArchitectureNameFormat = value;
        }

        public bool RemoveInputFile
        {
            get => options.RemoveInputFile;
            set => options.RemoveInputFile = value;
        }

        public override bool Execute()
        {
            if (!Enabled)
            {
                Log.LogMessage($"DllExports{nameof(Enabled)} was false. Nothing to do");
                return true;
            }

            if (string.IsNullOrWhiteSpace(InputFile))
            {
                Log.LogError($"DllExports{nameof(InputFile)} must be specified");
                return false;
            }

            Log.LogMessage(MessageImportance.Normal, $"Processing file '{InputFile}'");

            if (string.IsNullOrWhiteSpace(OutputFile))
            {
                Log.LogError($"DllExports{nameof(OutputFile)} must be specified");
                return false;
            }

            if (options.Architectures != null)
            {
                if (string.IsNullOrWhiteSpace(options.ArchitectureNameFormat))
                {
                    Log.LogError($"DllExports{nameof(ArchitectureNameFormat)} must be specified when DllExports{nameof(Architectures)} is specified");
                    return false;
                }

                var validArchs = new[]
                {
                    "I386",
                    "AMD64"
                };

                foreach (var arch in options.Architectures)
                {
                    if (!validArchs.Contains(arch, StringComparer.OrdinalIgnoreCase))
                    {
                        Log.LogError($"Invalid architecture '{arch}' specified. Valid architectures: {string.Join(", ", validArchs)}");
                        return false;
                    }
                }
            }

            var outputs = options.CalculateOutputFiles();

            if (options.RemoveInputFile && outputs.Any(o => StringComparer.OrdinalIgnoreCase.Equals(o.Path, options.InputFile)))
            {
                Log.LogError($"Cannot set option DllExports{nameof(RemoveInputFile)} to true when inputs and outputs are the same file");
                return false;
            }

            var taskRunner = new IsolatedTaskRunner();
            try
            {
                taskRunner.Execute(options);
            }
            catch (TargetInvocationException ex)
            {
                Log.LogErrorFromException(ex.InnerException);
                return false;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }

            if (options.RemoveInputFile)
                File.Delete(options.InputFile);

            var directory = Path.GetDirectoryName(options.InputFile);
            var dllExportsDll = Path.Combine(directory, "DllExports.dll");

            if (File.Exists(dllExportsDll))
                File.Delete(dllExportsDll);

            Log.LogMessage(MessageImportance.High, string.Empty);

            foreach (var output in outputs)
                Log.LogMessage(MessageImportance.High, $"DllExports ({output.Name}) -> {output.Path}");
            
            return true;
        }
    }
}
