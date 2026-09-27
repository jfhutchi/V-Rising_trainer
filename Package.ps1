#requires -Version 7.0
[CmdletBinding()]
param(
    [string] $GameDir = 'D:\SteamLibrary\steamapps\common\VRising'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'Build.ps1') -GameDir $GameDir
[xml] $project = Get-Content -LiteralPath (Join-Path $root 'HutchVRisingTrainer.csproj') -Raw
$version = [string] $project.Project.PropertyGroup.Version
$dll = Join-Path $root 'bin\Release\net6.0\HutchVRisingTrainer.dll'
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dll).Version
if ($assemblyVersion.ToString(3) -ne $version) { throw 'Built DLL version does not match the project.' }
$pluginSource = Get-Content -LiteralPath (Join-Path $root 'src\Plugin.cs') -Raw
if ($pluginSource -notmatch ('public const string Version = "' + [regex]::Escape($version) + '";')) {
    throw 'Plugin version does not match the project.'
}

$output = Join-Path $root 'dist\releases'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$zipPath = Join-Path $output "Hutch-V-Rising-Trainer-$version-Nexus.zip"
$checksumPath = "$zipPath.sha256"
if ((Test-Path -LiteralPath $zipPath) -or (Test-Path -LiteralPath $checksumPath)) {
    throw "Release already exists. Move it aside before rebuilding: $zipPath"
}

# An explicit allowlist keeps saves, local configuration, logs and build dependencies out.
$files = [ordered]@{
    'BepInEx/plugins/HutchVRisingTrainer/HutchVRisingTrainer.dll' = $dll
    'README.md' = (Join-Path $root 'packaging\INSTALL.md')
    'CHANGELOG.md' = (Join-Path $root 'CHANGELOG.md')
    'THIRD-PARTY-NOTICES.md' = (Join-Path $root 'THIRD-PARTY-NOTICES.md')
    'licenses/KindredCommands-AGPL-3.0.txt' = (Join-Path $root 'licenses\KindredCommands-AGPL-3.0.txt')
    'tools/Install-BloodAssist.ps1' = (Join-Path $root 'tools\Install-BloodAssist.ps1')
    'tools/Install-QualityOfLife.ps1' = (Join-Path $root 'tools\Install-QualityOfLife.ps1')
}
$sourceFiles = @('Build.ps1', 'Package.ps1', 'HutchVRisingTrainer.csproj', 'README.md',
    'CHANGELOG.md', 'THIRD-PARTY-NOTICES.md', '.gitignore')
foreach ($directory in @('src', 'data', 'tests', 'tools', 'packaging', 'licenses')) {
    $sourceFiles += @(Get-ChildItem -LiteralPath (Join-Path $root $directory) -File -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
            $_.Extension -in @('.cs', '.csproj', '.props', '.json', '.ps1', '.md', '.txt') } |
        ForEach-Object { [IO.Path]::GetRelativePath($root, $_.FullName) })
}
foreach ($source in $sourceFiles) {
    $files['source/' + $source.Replace('\', '/')] = Join-Path $root $source
}
foreach ($path in $files.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing release input: $path" }
}

Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $files.GetEnumerator()) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $entry.Value, $entry.Key, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }

$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    if ($archive.Entries.Count -ne $files.Count) { throw 'Release ZIP entry count mismatch.' }
    foreach ($entry in $archive.Entries) {
        $stream = $entry.Open()
        try { $actualHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
        finally { $stream.Dispose() }
        $expectedHash = (Get-FileHash -LiteralPath $files[$entry.FullName] -Algorithm SHA256).Hash
        if ($actualHash -ne $expectedHash) { throw "Release entry checksum mismatch: $($entry.FullName)" }
    }
}
finally { $archive.Dispose() }
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
"$zipHash  $([IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath $checksumPath -Encoding ascii
Write-Output "Verified $($files.Count) release entries: $zipPath"
Write-Output "SHA256: $zipHash"
