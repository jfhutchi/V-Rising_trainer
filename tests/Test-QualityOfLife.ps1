$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$script = Join-Path $root 'tools\Install-QualityOfLife.ps1'
$fixture = Join-Path $PSScriptRoot ('.quality-of-life-' + [guid]::NewGuid().ToString('N'))
$plugins = Join-Path $fixture 'BepInEx\plugins'
$helpers = Join-Path $plugins 'HutchVRisingQualityOfLife'

try {
    New-Item -ItemType Directory -Path $plugins -Force | Out-Null
    & $script -Install -GameDir $fixture -StackMultiplier 10 -MachineSpeedMultiplier 15 | Out-Null
    if (@(Get-ChildItem -LiteralPath $helpers -Filter '*.dll' -File).Count -ne 2) {
        throw 'Expected two quality-of-life DLLs.'
    }
    foreach ($configRoot in @('BepInEx\config', 'BepInEx_Server\config')) {
        $stack = Get-Content -LiteralPath (Join-Path $fixture "$configRoot\VStack.cfg") -Raw
        $rates = Get-Content -LiteralPath (Join-Path $fixture "$configRoot\VRates.cfg") -Raw
        if ($stack -notmatch '(?m)^Multiplier = 10$' -or
            $rates -notmatch '(?m)^CraftSpeedMultiplier = 15$' -or
            $rates -notmatch '(?m)^RefinementSpeedMultiplier = 15$' -or
            $rates -notmatch '(?m)^HarvestMultiplier = 1$') {
            throw "Initial quality-of-life config was incorrect in $configRoot"
        }
    }
    & $script -Configure -GameDir $fixture -StackMultiplier 100 -MachineSpeedMultiplier 20 | Out-Null
    foreach ($configRoot in @('BepInEx\config', 'BepInEx_Server\config')) {
        $stack = Get-Content -LiteralPath (Join-Path $fixture "$configRoot\VStack.cfg") -Raw
        $rates = Get-Content -LiteralPath (Join-Path $fixture "$configRoot\VRates.cfg") -Raw
        if ($stack -notmatch '(?m)^Multiplier = 100$' -or
            $rates -notmatch '(?m)^CraftSpeedMultiplier = 20$' -or
            $rates -notmatch '(?m)^RefinementSpeedMultiplier = 20$' -or
            $rates -notmatch '(?m)^HarvestMultiplier = 1$') {
            throw "Updated quality-of-life config was incorrect in $configRoot"
        }
    }
    & $script -Remove -GameDir $fixture | Out-Null
    if (Test-Path -LiteralPath $helpers) { throw 'Removal left quality-of-life DLLs behind.' }
    & $script -Install -GameDir $fixture -StackMultiplier 10 -MachineSpeedMultiplier 10 | Out-Null
    $reinstalledStack = Get-Content -LiteralPath (Join-Path $fixture 'BepInEx\config\VStack.cfg') -Raw
    if ($reinstalledStack -notmatch '(?m)^Multiplier = 10$') {
        throw 'Reinstall did not reuse the saved config.'
    }
    & $script -Remove -GameDir $fixture | Out-Null
    Write-Output 'Isolated quality-of-life install/configure/remove/reinstall passed.'
}
finally {
    $testsPath = [IO.Path]::GetFullPath($PSScriptRoot)
    $fixturePath = [IO.Path]::GetFullPath($fixture)
    if (-not $fixturePath.StartsWith($testsPath + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a fixture outside $testsPath"
    }
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
