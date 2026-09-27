[CmdletBinding(DefaultParameterSetName = 'Verify')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Install')]
    [switch] $Install,

    [Parameter(Mandatory, ParameterSetName = 'Configure')]
    [switch] $Configure,

    [Parameter(Mandatory, ParameterSetName = 'Remove')]
    [switch] $Remove,

    [Parameter(Mandatory, ParameterSetName = 'Verify')]
    [switch] $VerifyDownloads,

    [ValidateRange(1, 1000)]
    [int] $StackMultiplier = 10,

    [ValidateRange(1, 1000)]
    [int] $MachineSpeedMultiplier = 10,

    [string] $GameDir = 'D:\SteamLibrary\steamapps\common\VRising'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

$packages = @(
    @{
        Name = 'VStacks'; Version = '1.0.1'; Publisher = 'AETeam'
        Hash = '289F43D4FF31EBEEB016269C827FFA079DB3E697BF4BA623614F80BB242EDE97'
        Entry = 'BepInEx/plugins/VStack/VStack.dll'; File = 'VStack.dll'
    },
    @{
        Name = 'VRates'; Version = '1.0.0'; Publisher = 'AETeam'
        Hash = '77DCC81E9900E6D979F3B6AFCA01EA51844A5F45D2F5BCF565CE5E6737802B6F'
        Entry = 'BepInEx/plugins/VRates/VRates.dll'; File = 'VRates.dll'
    }
)

$pluginRoot = Join-Path $GameDir 'BepInEx\plugins'
$destination = Join-Path $pluginRoot 'HutchVRisingQualityOfLife'
$manifestPath = Join-Path $destination 'installed.json'
$clientConfig = Join-Path $GameDir 'BepInEx\config'
$serverConfig = Join-Path $GameDir 'BepInEx_Server\config'

function Read-InstalledPackage {
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "No quality-of-life manifest found at $manifestPath"
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $names = @($manifest.Files | ForEach-Object { $_.Name } | Sort-Object)
    if (($names -join '|') -ne 'VRates.dll|VStack.dll') {
        throw 'Quality-of-life manifest has unexpected files.'
    }
    $unexpected = @(Get-ChildItem -LiteralPath $destination -Force |
        Where-Object { $_.Name -notin ($names + 'installed.json') })
    if ($unexpected.Count -gt 0) {
        throw "Quality-of-life plugin directory contains unexpected files: $destination"
    }
    foreach ($file in $manifest.Files) {
        $path = Join-Path $destination $file.Name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Hash) {
            throw "Installed plugin is missing or changed: $path"
        }
    }
    return $manifest
}

function Set-ConfigValue([string] $path, [string] $key, [int] $value) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Expected config is missing: $path"
    }
    $contents = Get-Content -LiteralPath $path -Raw
    $pattern = '(?m)^' + [regex]::Escape($key) + '\s*=\s*[^\r\n]*'
    if (-not [regex]::IsMatch($contents, $pattern)) {
        throw "Config key $key is missing from $path"
    }
    $updated = [regex]::Replace($contents, $pattern, "$key = $value")
    Set-Content -LiteralPath $path -Value $updated -Encoding utf8 -NoNewline
}

if ($Remove -or $Configure -or $Install) {
    $gameRoot = [IO.Path]::GetFullPath($GameDir).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $runningHere = @(Get-Process -Name 'VRising', 'VRisingServer' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($gameRoot + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase) })
    if ($runningHere.Count -gt 0) {
        throw 'Close V Rising before changing stack or machine-speed plugins.'
    }
}

if ($Remove) {
    $manifest = Read-InstalledPackage
    foreach ($file in $manifest.Files) {
        Remove-Item -LiteralPath (Join-Path $destination $file.Name) -Force
    }
    Remove-Item -LiteralPath $manifestPath -Force
    Remove-Item -LiteralPath $destination
    Write-Output 'Removed quality-of-life DLLs. Their config files were kept for a future reinstall.'
    return
}

if ($Configure) {
    Read-InstalledPackage | Out-Null
    foreach ($configRoot in @($clientConfig, $serverConfig)) {
        Set-ConfigValue (Join-Path $configRoot 'VStack.cfg') 'Multiplier' $StackMultiplier
        Set-ConfigValue (Join-Path $configRoot 'VRates.cfg') 'CraftSpeedMultiplier' $MachineSpeedMultiplier
        Set-ConfigValue (Join-Path $configRoot 'VRates.cfg') 'RefinementSpeedMultiplier' $MachineSpeedMultiplier
    }
    Write-Output "Configured stacks x$StackMultiplier and crafting/refinement speed x$MachineSpeedMultiplier. Restart V Rising to apply."
    return
}

$payloads = @{}
foreach ($package in $packages) {
    $url = "https://thunderstore.io/package/download/$($package.Publisher)/$($package.Name)/$($package.Version)/"
    $temporaryZip = Join-Path ([IO.Path]::GetTempPath()) ("hutch-vrising-$([guid]::NewGuid().ToString('N')).zip")
    try {
        Invoke-WebRequest -Uri $url -OutFile $temporaryZip
        if ((Get-FileHash -LiteralPath $temporaryZip -Algorithm SHA256).Hash -ne $package.Hash) {
            throw "Download checksum mismatch for $($package.Name) $($package.Version)."
        }
        $archive = [IO.Compression.ZipFile]::OpenRead($temporaryZip)
        try {
            $manifestEntry = $archive.GetEntry('manifest.json')
            $dllEntry = $archive.GetEntry($package.Entry)
            if ($null -eq $manifestEntry -or $null -eq $dllEntry) {
                throw "Expected package entries are missing from $($package.Name)."
            }
            $reader = [IO.StreamReader]::new($manifestEntry.Open())
            try { $packageManifest = $reader.ReadToEnd() | ConvertFrom-Json }
            finally { $reader.Dispose() }
            if ($packageManifest.name -ne $package.Name -or
                $packageManifest.version_number -ne $package.Version) {
                throw "Package identity mismatch for $($package.Name)."
            }
            $memory = [IO.MemoryStream]::new()
            try {
                $stream = $dllEntry.Open()
                try { $stream.CopyTo($memory) }
                finally { $stream.Dispose() }
                $payloads[$package.File] = $memory.ToArray()
            }
            finally { $memory.Dispose() }
        }
        finally { $archive.Dispose() }
        Write-Output "Verified $($package.Name) $($package.Version)"
    }
    finally {
        if (Test-Path -LiteralPath $temporaryZip) { Remove-Item -LiteralPath $temporaryZip -Force }
    }
}

if ($VerifyDownloads) { return }

if (-not (Test-Path -LiteralPath $pluginRoot -PathType Container)) {
    throw "BepInEx plugins directory not found: $pluginRoot"
}
if (Test-Path -LiteralPath $destination) {
    throw "Quality-of-life plugin directory already exists: $destination. Use -Configure to change multipliers."
}

$configPaths = @(
    (Join-Path $clientConfig 'VStack.cfg'),
    (Join-Path $clientConfig 'VRates.cfg'),
    (Join-Path $serverConfig 'VStack.cfg'),
    (Join-Path $serverConfig 'VRates.cfg')
)
$existingConfigPaths = @($configPaths | Where-Object { Test-Path -LiteralPath $_ })
if ($existingConfigPaths.Count -ne 0 -and $existingConfigPaths.Count -ne $configPaths.Count) {
    throw 'Only some quality-of-life config files exist; refusing a partial install.'
}
$reuseConfigs = $existingConfigPaths.Count -eq $configPaths.Count
$originalConfigs = @{}
if ($reuseConfigs) {
    foreach ($path in $configPaths) { $originalConfigs[$path] = [IO.File]::ReadAllBytes($path) }
}

$stackConfigText = "[Stacks]`nMultiplier = $StackMultiplier`n"
$rateConfigText = @"
[Rates]
HarvestMultiplier = 1
LootMultiplier = 1
MissionLootMultiplier = 1
StygianShardMultiplier = 1
BloodEssenceMultiplier = 1
CraftSpeedMultiplier = $MachineSpeedMultiplier
RefinementSpeedMultiplier = $MachineSpeedMultiplier
RecipeCostMultiplier = 1
RefinementCostMultiplier = 1
BuildCostMultiplier = 1
"@

$createdFiles = @()
try {
    New-Item -ItemType Directory -Path $destination -ErrorAction Stop | Out-Null
    New-Item -ItemType Directory -Path $clientConfig, $serverConfig -Force | Out-Null
    $installedFiles = @()
    foreach ($name in $payloads.Keys) {
        $path = Join-Path $destination $name
        [IO.File]::WriteAllBytes($path, $payloads[$name])
        $createdFiles += $path
        $installedFiles += [pscustomobject]@{
            Name = $name
            Hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        }
    }
    foreach ($configRoot in @($clientConfig, $serverConfig)) {
        $stackPath = Join-Path $configRoot 'VStack.cfg'
        $ratePath = Join-Path $configRoot 'VRates.cfg'
        if ($reuseConfigs) {
            Set-ConfigValue $stackPath 'Multiplier' $StackMultiplier
            Set-ConfigValue $ratePath 'CraftSpeedMultiplier' $MachineSpeedMultiplier
            Set-ConfigValue $ratePath 'RefinementSpeedMultiplier' $MachineSpeedMultiplier
        }
        else {
            Set-Content -LiteralPath $stackPath -Value $stackConfigText -Encoding utf8 -NoNewline
            $createdFiles += $stackPath
            Set-Content -LiteralPath $ratePath -Value $rateConfigText -Encoding utf8 -NoNewline
            $createdFiles += $ratePath
        }
    }
    @{ Files = $installedFiles; InstalledUtc = (Get-Date).ToUniversalTime().ToString('o') } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $createdFiles += $manifestPath
}
catch {
    foreach ($path in $createdFiles) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    foreach ($path in $originalConfigs.Keys) { [IO.File]::WriteAllBytes($path, $originalConfigs[$path]) }
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination }
    throw
}

Write-Output "Installed stacks x$StackMultiplier and crafting/refinement speed x$MachineSpeedMultiplier. Restart V Rising to apply."
