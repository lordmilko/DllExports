using System;
using System.IO;
using DllExports.MSBuild;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DllExports.Tests
{
    [TestClass]
    public class ExporterTests
    {
        [TestMethod]
        public void Exporter_Success()
        {
            TestExporter(null);
        }

        [TestMethod]
        public void Exporter_Enabled_False()
        {
            TestExporter(t => t.Enabled = false);
        }

        [TestMethod]
        public void Exporter_Architectures_DefaultNameFormat()
        {
            TestExporter(t =>
            {
                t.Architectures = new[] {"i386"};
                t.ArchitectureNameFormat = "{name}.{arch}";
            });
        }

        [TestMethod]
        public void Exporter_Architectures_CustomNameFormat()
        {
            TestExporter(t =>
            {
                t.Architectures = new[] { "i386" };
                t.ArchitectureNameFormat = "{name}.foo.{arch}";
            });
        }

        [TestMethod]
        public void Exporter_InputFile_Empty_Throws()
        {
            var task = new GenerateDllExports
            {
                Enabled = true,
                InputFile = null
            };

            AssertEx.Throws<InvalidOperationException>(
                () => task.Execute(),
                "DllExportsInputFile must be specified"
            );
        }

        [TestMethod]
        public void Exporter_OutputFile_Empty_Throws()
        {
            var task = new GenerateDllExports
            {
                Enabled = true,
                InputFile = "foo"
            };

            task.BuildEngine = new MockBuildEngine();

            AssertEx.Throws<InvalidOperationException>(
                () => task.Execute(),
                "DllExportsOutputFile must be specified"
            );
        }

        [TestMethod]
        public void Exporter_ArchitectureNameFormat_WithArchitectures_Empty_Throws()
        {
            AssertEx.Throws<InvalidOperationException>(
                () =>
                {
                    TestExporter(t =>
                    {
                        t.Architectures = new[] { "i386" };
                        t.ArchitectureNameFormat = null;
                    });
                },
                "DllExportsArchitectureNameFormat must be specified when DllExportsArchitectures is specified"
            );
        }

        [TestMethod]
        public void Exporter_RemoveInputFile_SameInputAndOutput_Throws()
        {
            AssertEx.Throws<InvalidOperationException>(
                () => TestExporter(t => t.RemoveInputFile = true),
                "Cannot set option DllExportsRemoveInputFile to true when inputs and outputs are the same file"
            );
        }

        [TestMethod]
        public void Exporter_RemoveInputFile_ArchitectureSpecificExports()
        {
            TestExporter(t =>
            {
                t.Architectures = new[] { "i386" };
                t.ArchitectureNameFormat = "{name}.{arch}";
                t.RemoveInputFile = true;
            });
        }

        private void TestExporter(Action<GenerateDllExports> configure)
        {
            var assemblyPath = GetType().Assembly.Location;
            var directory = Path.GetDirectoryName(assemblyPath);

            var targetFramework = "netstandard2.0";

#if DEBUG
            var configuration = "Debug";
#else
            var configuration = "Release";
#endif

            var solution = Path.GetFullPath(Path.Combine(directory, "..", "..", "..", ".."));

            var testFile = Path.Combine(solution, "Samples", "SingleTarget", "bin", configuration, targetFramework, "SingleTarget.dll");

            if (!File.Exists(testFile))
                throw new InvalidOperationException($"Test File '{testFile}' was not found. Test project must be compiled prior to running tests");

            var tmpFile = Path.GetTempFileName();
            tmpFile = Path.ChangeExtension(tmpFile, ".dll");

            try
            {
                File.Copy(testFile, tmpFile, true);

                var task = new GenerateDllExports
                {
                    Enabled = true,
                    InputFile = tmpFile,
                    OutputFile = tmpFile,

                    BuildEngine = new MockBuildEngine()
                };

                configure?.Invoke(task);

                task.Execute();
            }
            finally
            {
                if (File.Exists(tmpFile))
                    File.Delete(tmpFile);
            }
        }
    }
}
