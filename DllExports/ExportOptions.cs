using System;
using System.Collections.Generic;
using System.IO;

namespace DllExports
{
    internal class ExportOptions
    {
        public bool Enabled { get; set; }

        public string InputFile { get; set; }

        public string OutputFile { get; set; }

        public string[] Architectures { get; set; }

        public string ArchitectureNameFormat { get; set; }

        public bool RemoveInputFile { get; set; }

        internal (string Path, string Name, bool? Is32Bit)[] CalculateOutputFiles()
        {
            var outputFile = GetOutputFileName();

            if (Architectures == null || Architectures.Length == 0)
                return new (string Path, string Name, bool? Is32Bit)[] { (outputFile, "Default", null) };

            var results = new List<(string Path, string Name, bool? Is32Bit)>();

            foreach (var arch in Architectures)
            {
                var outputDir = Path.GetDirectoryName(outputFile);
                var baseName = Path.GetFileNameWithoutExtension(outputFile);
                var ext = Path.GetExtension(outputFile);

                string displayArch;
                bool is32Bit;

                switch (arch.ToLower())
                {
                    case "i386":
                        displayArch = "x86";
                        is32Bit = true;
                        break;

                    case "amd64":
                        displayArch = "x64";
                        is32Bit = false;
                        break;

                    default:
                        throw new NotSupportedException($"Architecture '{arch}' is not supported");
                }

                var newName = Path.Combine(
                    outputDir,
                    ArchitectureNameFormat.Replace("{name}", baseName).Replace("{arch}", displayArch) + ext
                );

                results.Add((newName, displayArch, is32Bit));
            }

            return results.ToArray();
        }

        private string GetOutputFileName()
        {
            var directory = Path.GetDirectoryName(OutputFile);

            var outputFile = OutputFile;

            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = Path.GetDirectoryName(InputFile);
                var fileName = Path.GetFileName(OutputFile);
                outputFile = Path.Combine(directory, fileName);
            }

            if (string.IsNullOrWhiteSpace(Path.GetExtension(outputFile)))
                outputFile += Path.GetExtension(InputFile);

            return outputFile;
        }
    }
}