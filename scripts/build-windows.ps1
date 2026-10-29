<#
.SYNOPSIS
    The Windows-only half of the checks: builds the real web project with MSBuild and precompiles the ASPX pages.

.DESCRIPTION
    tests/run-local.sh covers everything that works on Linux. This script covers what needs Windows:
      1. MSBuild of HostelManagement.sln (the true compiler for the web application project)
      2. aspnet_compiler, which compiles every .aspx/.master/.ascx and catches broken markup, unknown
         controls and handlers that do not exist before they fail at run time.
    It needs Visual Studio (or the Build Tools) with the ASP.NET workload and NuGet; nothing is installed or changed
    outside the repository. A placeholder connectionStrings.config is created only if you do not have one.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\build-windows.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'HostelManagement'

function Find-MSBuild {
    $onPath = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($found) { return $found }
    }
    throw 'MSBuild was not found. Install Visual Studio or the Build Tools with the "ASP.NET and web development" workload, or run this from a Developer PowerShell.'
}

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit code $LASTEXITCODE)." }
}

$msbuild = Find-MSBuild
Write-Host "Using $msbuild"

# The real connection string is git-ignored; neither step connects to a database, so the template is enough.
$connectionStrings = Join-Path $project 'connectionStrings.config'
$createdPlaceholder = $false
if (-not (Test-Path $connectionStrings)) {
    Copy-Item (Join-Path $project 'connectionStrings.config.example') $connectionStrings
    $createdPlaceholder = $true
}

$precompiled = Join-Path ([IO.Path]::GetTempPath()) ('hostelhub-precompiled-' + [Guid]::NewGuid().ToString('N'))
try {
    Invoke-Step 'Restore NuGet packages' {
        $nuget = Get-Command nuget -ErrorAction SilentlyContinue
        if ($nuget) { & $nuget.Source restore (Join-Path $repo 'HostelManagement.sln') }
        else { & $msbuild (Join-Path $repo 'HostelManagement.sln') -t:restore -p:RestorePackagesConfig=true -v:minimal }
    }

    Invoke-Step "MSBuild ($Configuration)" {
        & $msbuild (Join-Path $repo 'HostelManagement.sln') "-p:Configuration=$Configuration" -m -v:minimal
    }

    Invoke-Step 'Precompile ASPX markup' {
        $aspnetCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe'
        & $aspnetCompiler -v / -p $project -f -errorstack $precompiled
    }

    Write-Host "`nALL WINDOWS CHECKS PASSED" -ForegroundColor Green
}
finally {
    if (Test-Path $precompiled) { Remove-Item $precompiled -Recurse -Force }
    if ($createdPlaceholder) { Remove-Item $connectionStrings -Force }
}
