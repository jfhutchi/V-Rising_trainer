[CmdletBinding()]
param(
    [switch] $Install,
    [string] $GameDir = 'D:\SteamLibrary\steamapps\common\VRising'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'HutchVRisingTrainer.csproj'
& dotnet build $project -c Release -p:VRI_GAME_DIR="$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'Trainer build failed.' }

if ($Install) {
    if (Get-Process -Name 'VRising', 'VRisingServer' -ErrorAction SilentlyContinue) {
        throw 'Close V Rising before installing the new DLL.'
    }
    $destination = Join-Path $GameDir 'BepInEx\plugins\HutchVRisingTrainer'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'bin\Release\net6.0\HutchVRisingTrainer.dll') -Destination $destination -Force
    Write-Output "Installed trainer DLL in $destination"
}
