$ErrorActionPreference = "Stop"
$dllExportsMSBuild = Join-Path $PSScriptRoot DllExports.MSBuild
$dllExportsMSBuildBin = Join-Path (Join-Path $dllExportsMSBuild "bin") "Release"

function build
{
    dotnet build $dllExportsMSBuild -c Release
}

function pack
{
    gci $PSScriptRoot *.nupkg -Recurse | foreach { Remove-Item $_.FullName -Force }

    dotnet pack -c Release $dllExportsMSBuild    

    $nupkg = gci $dllExportsMSBuildBin *.nupkg

    $originalExtension = $nupkg.Extension
    $newName = $nupkg.Name -replace $originalExtension,".zip"
    $newPath = Join-Path $nupkg.DirectoryName $newName

    if (Test-Path $newPath)
    {
        Remove-item $newPath
    }

    $extractFolder = $nupkg.FullName -replace $nupkg.Extension,""

    if (Test-Path $extractFolder)
    {
        Remove-Item $extractFolder -Recurse -Force
    }

    try
    {
        $newItem = Rename-Item -Path $nupkg.FullName -NewName $newName -PassThru

        Expand-Archive $newItem.FullName $extractFolder

        $libDir = Join-Path $extractFolder "lib"
        $tasksDir = Join-Path $extractFolder "tasks"

        Copy-Item $libDir $tasksDir -Recurse

        $badDlls = gci $libDir -Recurse -File | where Name -ne "DllExports.dll"
        $badDlls | foreach { Remove-Item $_.FullName -Force }

        # Tasks are not supported on netstandard2.0
        Remove-Item (Join-Path $tasksDir "netstandard2.0") -Recurse -Force

        # We don't need libs for net472 and net5.0
        Remove-Item (Join-path $libDir "net472") -Recurse -Force
        Remove-Item (Join-path $libDir "net5.0") -Recurse -Force

        $nuspecFile = Join-Path $extractFolder "DllExports.nuspec"

        $lines = gc $nuspecFile
        $newLines = $lines | where { !$_.Contains(".NETFramework4.7.2") -and !$_.Contains("net5.0") }
        $newLines | Set-Content $nuspecFile

        gci $extractFolder | Compress-Archive -DestinationPath $newItem.FullName -Force
    }
    finally
    {
        Remove-Item $extractFolder -Recurse -Force
        Rename-Item $newItem.FullName $nupkg.Name
    }

    $destination = Join-Path $PSScriptRoot $nupkg.Name
    Move-item $nupkg.FullName $destination

    Write-Host "NuGet package created at $destination" -ForegroundColor Green
}

function test
{
    [CmdletBinding()]
    param(
        [Parameter(Position=0)]
        [string[]]$Samples,

        [string]$Configuration = "Release"
    )

    $kernel32 = ([System.Management.Automation.PSTypeName]"PInvoke.Kernel32").Type

    if(!$kernel32)
    {
        $str = @"
[DllImport("kernel32.dll", SetLastError = true)]
public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

[DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
public static extern IntPtr LoadLibrary(string lpLibFileName);

[DllImport("kernel32.dll", SetLastError = true)]
public static extern bool FreeLibrary(IntPtr hLibModule);
"@

        $kernel32 = Add-Type -Name Kernel32 -MemberDefinition $str -Namespace "PInvoke" -PassThru
    }
    

    $nupkg = gci $PSScriptRoot *.nupkg

    if(!$nupkg)
    {
        throw "Package must be built before running tests"
    }

    $repoPath = Join-Path $env:temp DllExportsTempRepo
    $nugetConfigPath = Join-Path $PSScriptRoot "NuGet.config"

    # Cleanup any files from previous runs

    if(Test-Path $repoPath)
    {
        Remove-Item $repoPath -Recurse -Force
    }

    if(Test-Path $nugetConfigPath)
    {
        Remove-Item $nugetConfigPath -Recurse -Force
    }

    $nugetCache = Join-Path (Join-Path (Join-Path $env:USERPROFILE .nuget) "packages") "dllexports"

    if(Test-Path $nugetCache)
    {
        Remove-Item $nugetCache -Recurse -Force
    }

    try
    {
        # Add a config file pointing to a local repo containing our nupkg

        nuget add $nupkg.FullName -Source $repoPath

        $nugetConfig = @"
<configuration>
	<packageSources>
		<add key="local" value="$repoPath" />
	</packageSources>
</configuration>
"@

        Set-Content $nugetConfigPath $nugetConfig

        $sampleDirs = gci $PSScriptRoot\Samples

        foreach($sample in $sampleDirs)
        {
            if($Samples -ne $null -and $sample.Name -notin $Samples)
            {
                continue
            }

            if ($sample.Name -eq "NetFramework")
            {
                $csproj = Join-Path $sample.FullName "NetFramework.csproj"
                $csprojContent = gc $csproj -Raw
                $dllExportVersion = [regex]::Match($csprojContent, ".+?(DllExports\..+?)\\").groups[1].Value

                $newContent = $csprojContent -replace $dllExportVersion,$nupkg.BaseName
                Set-Content $csproj $newContent -NoNewline -Encoding UTF8

                $packagesConfig = Join-Path $sample.FullName "packages.config"
                $packagesConfigContent = gc $packagesConfig -Raw
                $oldVersion = $dllExportVersion -replace "DllExports.",""
                $newVersion = $nupkg.BaseName -replace "DllExports.",""
                $newContent = $packagesConfigContent -replace $oldVersion,$newVersion
                Set-Content $packagesConfig $newContent -NoNewline -Encoding UTF8

                nuget restore $csproj -SolutionDirectory $PSScriptRoot
            }

            Write-Host "Testing sample $($sample.FullName)" -ForegroundColor Magenta

            $obj = Join-Path $sample.FullName obj
            $bin = Join-Path $sample.FullName bin

            if (Test-Path $obj)
            {
                Remove-Item $obj -Recurse -Force
            }

            if (Test-Path $bin)
            {
                Remove-Item $bin -Recurse -Force
            }

            dotnet build $sample.FullName -c $Configuration /p:DllExportsArchitectureNameFormat="{name}" /p:DllExportsArchitectures=AMD64

            if($? -eq $false)
            {
                throw "$($sample.Name) build failed"
            }

            $binDir = Join-Path $bin $Configuration

            $targetFrameworks = gci $binDir -Directory

            if($targetFrameworks.Length -eq 0)
            {
                # .NET Framework
                $targetFrameworks = @(gi $binDir)
            }

            foreach($targetFramework in $targetFrameworks)
            {
                Write-Host "    Checking $targetFramework" -ForegroundColor Cyan

                $dll = gci $targetFramework.FullName *.dll

                if(@($dll).Count -ne 1)
                {
                    throw "Expected exactly 1 DLL but this was not the case"
                }

                $hModule = $kernel32::LoadLibrary($dll.FullName)

                try
                {
                    if($hModule)
                    {
                        $export = $kernel32::GetProcAddress($hModule, "MyExport")

                        if($export -ne 0)
                        {
                            Write-Host "        Found 'MyExport' at 0x$($export.ToString('X'))" -ForegroundColor Green
                        }
                        else
                        {
                            throw "Failed to find 'MyExport' in sample '$($sample.Name)/$($targetFramework.Name)'"
                        }
                    }
                    else
                    {
                        throw "Failed to load $($dll.FullName)"
                    }
                }
                finally
                {
                    $kernel32::FreeLibrary($hModule) | Out-Null
                }
            }
        }
    }
    finally
    {
        if(Test-Path $repoPath)
        {
            Remove-Item $repoPath -Recurse -Force
        }

        if(Test-Path $nugetConfigPath)
        {
            Remove-Item $nugetConfigPath -Recurse -Force
        }
    }
}
