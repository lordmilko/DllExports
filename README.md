# DllExports

[![Appveyor status](https://ci.appveyor.com/api/projects/status/mgsa6414kmv4aoko?svg=true)](https://ci.appveyor.com/project/lordmilko/dllexports)
[![NuGet](https://img.shields.io/nuget/v/DllExports.svg)](https://www.nuget.org/packages/DllExports/)

*All I want in life is a package to enable unmanaged exports via a simple MSBuild task in my .NET application*

There are several packages out there that attempt to facilitate enabling unmanaged exports in .NET applications. A big challenge with existing solutions however is that either:

* They don't work
* They require a bunch of wacky external dependencies
* They won't work because they require a bunch of wacky external dependencies

DllExports aims to be a simple .NET package that:

1. Works
2. Doesn't require any external dependencies

Thus hopefully ensuring it continues to work into the future.

**In order to be able to debug your exports in Visual Studio you must be targeting .NET Framework. .NET Standard exports work but you can't debug them
(presumably because it expected the .NET Core runtime to be loaded but .NET Framework was loaded instead). .NET Core applications can't truly have unmanaged
exports as you can't use mscoree to load their runtime. Consider using a library such as [DNNE](https://github.com/AaronRobinsonMSFT/DNNE) for proper .NET Core support
(however this will require C++ tooling to be properly installed).**

Please see [Tips](#tips) below for some important gotchas to be aware of.

## Usage

To declare an unmanaged export, simply decorate a static method with `DllExportAttribute`

```c#
using DllExports;

[DllExport]
public static void Foo()
{
}
```
You may optionally also specify the name and calling convention to use for the unmanaged export. If no calling convention is specified, by default `stdcall` will be used.

DllExports provides a number of knobs you can use to adjust how your input file will be processed.

| Property                         | Default Value            | Description                                                               |
| -------------------------------- | ------------------------ | ------------------------------------------------------------------------- |
| DllExportsEnabled                | `true`                   | Whether DllExports should process unmanaged exports upon building         |
| DllExportsInputFile              | `$(TargetPath)`          | The file DllExports should process unmanaged exports for                  |
| DllExportsOutputFile             | `$(DllExportsInputFile)` | The file to save the modified file as                                     |
| DllExportsArchitectures          |                          | Architectures to generate DllExports for. e.g. `i386;AMD64`. Each architecture will get its own file. If no architecture is specified, the architecture is not modified |
| DllExportsArchitectureNameFormat | `{name}.{arch}`          | The name format used when processing `DllExportsArchitectures`. Resulting filename will be `DllExportsOutputFile` directory + `DllExportsArchitectureNameFormat` + file extension |
| DllExportsRemoveInputFile        | `false`                  | Whether to remove `DllExportsInputFile` upon generating unmanaged exports |

DllExports currently only supports generating unmanaged exports for i386 and AMD64.

When you compile a library as AnyCPU, implicitly it is actually either i386 or AMD64, and will only be loaded properly in an application with a matching architecture.

## Legacy Projects

Historically, packages have installed MSBuild props and targets via a PowerShell init script embedded within the NuGet package. Such scripts do not work, and are
essentially unnecessary when it comes to SDK style projects. As SDK style projects are a lot easier to manage than legacy style projects, it makes sense to use
SDK style projects even if you are building applications against .NET Framework. As such, DllExports does not provide a script for automatically updating your project file
for seamless support with legacy style projects. You can easily inject the required changes yourself however, as follows:

1. Insert the `props` import after all other props imports at the top of the file

```xml
<Import Project="..\..\packages\DllExports.0.1.1\build\DllExports.props" Condition="Exists('..\..\packages\DllExports.0.1.1\build\DllExports.targets')" />
```
2. Add a package reference, with `Private = False` so DllExports does not get emitted to your output directory

```xml
<Reference Include="DllExports">
  <HintPath>..\..\packages\DllExports.0.1.1\lib\netstandard2.0\DllExports.dll</HintPath>
  <Private>False</Private>
</Reference>
```
3. Import the `targets` at the end of the file and add a `Target` to warn when NuGet packages have not been restored

```xml
<Import Project="..\..\packages\DllExports.0.1.1\build\DllExports.targets" Condition="Exists('..\..\packages\DllExports.0.1.1\build\DllExports.targets')" />
<Target Name="EnsureNuGetPackageBuildImports" BeforeTargets="PrepareForBuild">
  <PropertyGroup>
    <ErrorText>This project references NuGet package(s) that are missing on this computer. Use NuGet Package Restore to download them.  For more information, see http://go.microsoft.com/fwlink/?LinkID=322105. The missing file is {0}.</ErrorText>
  </PropertyGroup>
  <Error Condition="!Exists('..\..\packages\DllExports.0.1.1\build\DllExports.targets')" Text="$([System.String]::Format('$(ErrorText)', '..\..\packages\DllExports.0.1.1\build\DllExports.targets'))" />
</Target>
```

Adjust the version number in the above snippets as necessary. The [NetFramework](https://github.com/lordmilko/DllExports/blob/master/Samples/NetFramework/NetFramework.csproj) sample demonstrates how your file should look like.

## Tips

* You cannot use projects that generate Portable PDB files together with DllExports in Visual Studio 2017. Something about the modifications that dnlib (which DllExports uses internally) makes upsets Visual Studio when it goes to load
the modified PDB file, and crashes the entire program. As such, DllExports will throw an error if it detects you are using portable/embedded PDB files in Visual Studio 2017, and recommend you use `<DebugType>full</DebugType>` instead.
Newer versions of Visual Studio do not have this issue. It's not clear whether Visual Studio 2017 or dnlib is failing to follow the Portable PDB file format properly. Legacy style projects default to *full* PDB files, while SDK style projects
default to *portable*.
* Don't use types types external to your assembly or the CLR in the method signature of your exports. e.g. do not use the `HRESULT` type from [ClrDebug](https://github.com/lordmilko/ClrDebug). The runtime is not in a position to load
external assemblies when your export is called. You can however use types defined in the same assembly that your export is defined in.
    * Once an external assembly has been loaded, it is safe to use types in external external assemblies in subsequently called exports
* You can force architecture specific files to be placed in an architecture specific subdirectory by setting `DllExportsArchitectureNameFormat` to something like `{arch}\{name}.{arch}` i.e. `Foo.dll` compiled for AMD64 will go to `x64\Foo.x64.dll`
* When multi-targeting, you can conditionally generate unmanaged exports for compatible assemblies as follows

    ```xml
	<TargetFrameworks>netstandard2.0;net472</TargetFrameworks>
	
    <DllExportsEnabled>false</DllExportsEnabled>
    <DllExportsEnabled Condition="'$(TargetFramework)' == 'net472'">true</DllExportsEnabled>
    ```
* When consuming third party libraries in your unmanaged export, watch out for assembly resolution issues! If you exported assembly is loaded into some other application,
when you attempt to reference a type in a third party library, the CLR is going to look in the directory of *that application* - **not** the directory that your assembly and all
its dependencies are in. Consider setting `AppDomain.CurrentDomain.AssemblyResolve` and/or pre-emptively loading your assemblies in the first export accessed via `Assembly.LoadFrom`
    * The CLR will only attempt to load an assembly when a type within it is referenced within a given method. If your program relies on an outer method doing assembly resolution prior
	to calling an inner method, consider decorating the inner method with `[MethodImpl(MethodImplOptions.NoInlining)]`