$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$script = Join-Path $root 'tools\Install-BloodAssist.ps1'
$fixture = Join-Path $PSScriptRoot ('.blood-assist-' + [guid]::NewGuid().ToString('N'))
$plugins = Join-Path $fixture 'BepInEx\plugins'

try {
    New-Item -ItemType Directory -Path $plugins -Force | Out-Null
    & $script -Install -GameDir $fixture | Out-Null
    $helpers = Join-Path $plugins 'HutchVRisingServerHelpers'
    if (@(Get-ChildItem -LiteralPath $helpers -Filter '*.dll' -File).Count -ne 4) {
        throw 'Expected four server helper DLLs.'
    }

    Remove-Item -LiteralPath (Join-Path $helpers 'KindredSchematics.dll')
    $manifestPath = Join-Path $helpers 'installed.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $manifest.Files = @($manifest.Files | Where-Object { $_.Name -ne 'KindredSchematics.dll' })
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    & $script -Install -GameDir $fixture | Out-Null
    if (@(Get-ChildItem -LiteralPath $helpers -Filter '*.dll' -File).Count -ne 4) {
        throw 'Upgrade did not restore the fourth server helper.'
    }

    & $script -Remove -GameDir $fixture | Out-Null
    if (Test-Path -LiteralPath $helpers) { throw 'Removal left the helper directory behind.' }
    Write-Output 'Isolated server helper install/upgrade/remove passed.'
}
finally {
    $testsPath = [IO.Path]::GetFullPath($PSScriptRoot)
    $fixturePath = [IO.Path]::GetFullPath($fixture)
    if (-not $fixturePath.StartsWith($testsPath + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a fixture outside $testsPath"
    }
    if (Test-Path -LiteralPath $plugins) { Remove-Item -LiteralPath $plugins }
    $bepInEx = Join-Path $fixture 'BepInEx'
    if (Test-Path -LiteralPath $bepInEx) { Remove-Item -LiteralPath $bepInEx }
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture }
}
