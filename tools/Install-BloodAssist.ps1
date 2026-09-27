[CmdletBinding(DefaultParameterSetName = 'Verify')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Install')]
    [switch] $Install,

    [Parameter(Mandatory, ParameterSetName = 'Remove')]
    [switch] $Remove,

    [Parameter(Mandatory, ParameterSetName = 'Verify')]
    [switch] $VerifyDownloads,

    [string] $GameDir = 'D:\SteamLibrary\steamapps\common\VRising'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$packages = @(
    @{
        Name = 'ServerLaunchFix'; Version = '0.2.0'; Publisher = 'Mythic'
        Hash = 'A9C4F101A754CFB9F5371B00DEBB6487FCCCE62539F59BD77300CC1E2AD19396'
        Files = @('ServerLaunchFix.dll')
    },
    @{
        Name = 'VampireCommandFramework'; Version = '0.10.4'; Publisher = 'deca'
        Hash = 'E554E964242DB0B1CFEF5824129AA6794C8A7138C9321B617D633F9AAFFAC19B'
        Files = @('VampireCommandFramework.dll')
    },
    @{
        Name = 'KindredCommands'; Version = '2.5.8'; Publisher = 'odjit'
        Hash = '80B9CF909389A4869F45B052E393164079DB82A68E962F4A18A2619B0924E7B1'
        Files = @('KindredCommands.dll')
    },
    @{
        Name = 'KindredSchematics'; Version = '1.5.6'; Publisher = 'odjit'
        Hash = 'B78254420D6BA28EBC11E9F6706680FA61275BDC890361175C4C4F92A407C4BC'
        Files = @('KindredSchematics.dll')
    }
)

$pluginRoot = Join-Path $GameDir 'BepInEx\plugins'
$destination = Join-Path $pluginRoot 'HutchVRisingServerHelpers'
$manifestPath = Join-Path $destination 'installed.json'
$legacyNames = @('KindredCommands.dll', 'ServerLaunchFix.dll', 'VampireCommandFramework.dll')
$allNames = @($packages | ForEach-Object { $_.Files } | Sort-Object)

function Read-InstalledHelpers {
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "No Hutch server helper manifest was found at $manifestPath"
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $names = @($manifest.Files | ForEach-Object { $_.Name } | Sort-Object)
    if (($names -join '|') -ne ($legacyNames -join '|') -and
        ($names -join '|') -ne ($allNames -join '|')) {
        throw 'Server helper manifest has unexpected file names.'
    }
    $unexpected = @(Get-ChildItem -LiteralPath $destination -Force |
        Where-Object { $_.Name -notin ($names + 'installed.json') })
    if ($unexpected.Count -gt 0) {
        throw "Server helper directory contains other files: $destination"
    }
    foreach ($file in $manifest.Files) {
        $path = Join-Path $destination $file.Name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Expected installed helper is missing: $path"
        }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Hash) {
            throw "Installed helper changed since installation: $path"
        }
    }
    return $manifest
}

if ($Remove) {
    if (Get-Process -Name 'VRising', 'VRisingServer' -ErrorAction SilentlyContinue) {
        throw 'Close V Rising before removing server helpers.'
    }
    $manifest = Read-InstalledHelpers
    foreach ($file in $manifest.Files) {
        Remove-Item -LiteralPath (Join-Path $destination $file.Name) -Force
    }
    Remove-Item -LiteralPath $manifestPath -Force
    Remove-Item -LiteralPath $destination
    Write-Output "Removed the optional server helpers from $destination"
    return
}

$upgrade = $false
if ($Install) {
    if (Get-Process -Name 'VRising', 'VRisingServer' -ErrorAction SilentlyContinue) {
        throw 'Close V Rising before installing server helpers.'
    }
    if (-not (Test-Path -LiteralPath $pluginRoot -PathType Container)) {
        throw "BepInEx plugins directory was not found: $pluginRoot"
    }
    if (Test-Path -LiteralPath $destination) {
        $existing = Read-InstalledHelpers
        $installedNames = @($existing.Files | ForEach-Object { $_.Name } | Sort-Object)
        if (($installedNames -join '|') -eq ($allNames -join '|')) {
            throw "All server helpers are already installed at $destination"
        }
        $upgrade = $true
    }
}

$payloads = @{}
foreach ($package in $packages) {
    $url = "https://thunderstore.io/package/download/$($package.Publisher)/$($package.Name)/$($package.Version)/"
    $temporaryZip = Join-Path ([IO.Path]::GetTempPath()) ("hutch-vrising-$([guid]::NewGuid().ToString('N')).zip")
    try {
        Invoke-WebRequest -Uri $url -OutFile $temporaryZip
        $actualHash = (Get-FileHash -LiteralPath $temporaryZip -Algorithm SHA256).Hash
        if ($actualHash -ne $package.Hash) {
            throw "Download checksum mismatch for $($package.Name) $($package.Version)."
        }
        $archive = [IO.Compression.ZipFile]::OpenRead($temporaryZip)
        try {
            $manifestEntry = $archive.GetEntry('manifest.json')
            if ($null -eq $manifestEntry) { throw "Package manifest missing: $($package.Name)" }
            $reader = [IO.StreamReader]::new($manifestEntry.Open())
            try { $packageManifest = $reader.ReadToEnd() | ConvertFrom-Json }
            finally { $reader.Dispose() }
            if ($packageManifest.name -ne $package.Name -or
                $packageManifest.version_number -ne $package.Version) {
                throw "Package identity mismatch: $($package.Name)"
            }
            foreach ($file in $package.Files) {
                $entry = $archive.GetEntry($file)
                if ($null -eq $entry) { throw "Missing package file $file in $($package.Name)" }
                $memory = [IO.MemoryStream]::new()
                try {
                    $inputStream = $entry.Open()
                    try { $inputStream.CopyTo($memory) }
                    finally { $inputStream.Dispose() }
                    $payloads[$file] = $memory.ToArray()
                }
                finally { $memory.Dispose() }
            }
        }
        finally { $archive.Dispose() }
        Write-Output "Verified $($package.Name) $($package.Version)"
    }
    finally {
        if (Test-Path -LiteralPath $temporaryZip) { Remove-Item -LiteralPath $temporaryZip -Force }
    }
}

if ($VerifyDownloads) { return }

if (-not $upgrade) { New-Item -ItemType Directory -Path $destination -ErrorAction Stop | Out-Null }
$installedFiles = @()
if ($upgrade) { $installedFiles = @($existing.Files) }
$createdPaths = @()
try {
    foreach ($name in $payloads.Keys) {
        if ($upgrade -and $name -ne 'KindredSchematics.dll') { continue }
        $path = Join-Path $destination $name
        $createdPaths += $path
        [IO.File]::WriteAllBytes($path, $payloads[$name])
        $installedFiles += [pscustomobject]@{
            Name = $name
            Hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        }
    }
    @{ Files = $installedFiles; InstalledUtc = (Get-Date).ToUniversalTime().ToString('o') } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding utf8
}
catch {
    foreach ($path in $createdPaths) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    if (Test-Path -LiteralPath $manifestPath) { Remove-Item -LiteralPath $manifestPath -Force }
    if (-not $upgrade) { Remove-Item -LiteralPath $destination }
    throw
}
Write-Output "Installed optional local server helpers in $destination"
Write-Output 'Restart V Rising. Use adminauth in the console, then enter KindredCommands in chat.'
