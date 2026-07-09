$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'mcp-servers-for-revit.sln'
$buildConfiguration = 'Release R24'
$commandSetPackageRoot = Join-Path $root 'plugin/bin/AddIn 2024 Release R24/revit_mcp_plugin/Commands/RevitMCPCommandSet'
$commandSetSourcePath = Join-Path $commandSetPackageRoot '2024'
$commandSetDeploymentRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024\revit_mcp_plugin\Commands\RevitMCPCommandSet'
$commandSetDestinationPath = Join-Path $commandSetDeploymentRoot '2024'
$pluginSourcePath = Join-Path $root 'plugin/bin/Release/2024/RevitMCPPlugin.dll'
$pluginDestinationDir = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024\revit_mcp_plugin'
$pluginDestinationPath = Join-Path $pluginDestinationDir 'RevitMCPPlugin.dll'
$commandsDestinationDir = Join-Path $pluginDestinationDir 'Commands'
$manifestSourcePath = Join-Path $commandSetPackageRoot 'command.json'
$manifestDestinationPath = Join-Path $commandSetDeploymentRoot 'command.json'
$registryDestinationPath = Join-Path $commandsDestinationDir 'commandRegistry.json'

function Write-CommandRegistryFromManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,

        [string]$CommandSetFolderName = 'RevitMCPCommandSet',

        [string]$RevitVersion = '2024'
    )

    if (-not (Test-Path $ManifestPath)) {
        throw "Command manifest not found at $ManifestPath"
    }

    $manifest = Get-Content -Raw -Path $ManifestPath | ConvertFrom-Json
    $commandEntries = @()

    foreach ($command in $manifest.commands) {
        $relativeAssemblyPath = if (-not [string]::IsNullOrWhiteSpace($command.assemblyPath)) { $command.assemblyPath } else { 'RevitMCPCommandSet.dll' }
        $commandEntries += [pscustomobject]@{
            commandName = $command.commandName
            assemblyPath = "$CommandSetFolderName\{VERSION}\$relativeAssemblyPath"
            enabled = $true
            supportedRevitVersions = @($RevitVersion)
            developer = $manifest.developer
            description = $command.description
        }
    }

    $registry = [pscustomobject]@{
        Commands = $commandEntries
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $DestinationPath) -Force | Out-Null
    $registry | ConvertTo-Json -Depth 10 | Set-Content -Path $DestinationPath -Encoding UTF8
}

Write-Host "Building $solution ($buildConfiguration)" -ForegroundColor Cyan
Push-Location $root
try {
    dotnet build $solution -c $buildConfiguration
}
finally {
    Pop-Location
}

# Close all Revit processes before copying to avoid file locking issues
Write-Host "Closing all Revit processes..." -ForegroundColor Yellow
Get-Process | Where-Object { $_.ProcessName -like "*Revit*" } | ForEach-Object {
    Write-Host "Stopping process $($_.ProcessName) (PID:$($_.Id))" -ForegroundColor Yellow
    Stop-Process -Id $_.Id -Force
}

if (-not (Test-Path $commandSetSourcePath)) {
    throw "Build output not found at $commandSetSourcePath"
}

if (-not (Test-Path $pluginSourcePath)) {
    throw "Plugin DLL not found at $pluginSourcePath"
}

if (-not (Test-Path $manifestSourcePath)) {
    throw "Command manifest not found at $manifestSourcePath"
}

New-Item -ItemType Directory -Path $commandSetDestinationPath -Force | Out-Null
New-Item -ItemType Directory -Path $commandSetDeploymentRoot -Force | Out-Null
New-Item -ItemType Directory -Path $pluginDestinationDir -Force | Out-Null
New-Item -ItemType Directory -Path $commandsDestinationDir -Force | Out-Null

Write-Host "Copying command set build output from $commandSetSourcePath to $commandSetDestinationPath" -ForegroundColor Cyan
Copy-Item -Path (Join-Path $commandSetSourcePath '*') -Destination $commandSetDestinationPath -Recurse -Force

Write-Host "Copying command manifest from $manifestSourcePath to $manifestDestinationPath" -ForegroundColor Cyan
Copy-Item -Path $manifestSourcePath -Destination $manifestDestinationPath -Force

Write-Host "Generating runtime command registry at $registryDestinationPath" -ForegroundColor Cyan
Write-CommandRegistryFromManifest -ManifestPath $manifestSourcePath -DestinationPath $registryDestinationPath

try {
    Write-Host "Copying plugin DLL from $pluginSourcePath to $pluginDestinationPath" -ForegroundColor Cyan
    Copy-Item -Path $pluginSourcePath -Destination $pluginDestinationPath -Force
}
catch {
    Write-Warning "Could not overwrite the plugin DLL because it is in use. Close Revit and rerun the script if needed. $($_.Exception.Message)"
}

Write-Host 'Deployment complete.' -ForegroundColor Green
