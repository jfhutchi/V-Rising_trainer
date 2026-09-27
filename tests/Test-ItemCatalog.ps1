$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $root 'bin\Release\net6.0\HutchVRisingTrainer.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw 'Build the trainer before running this test.' }

$assembly = [Reflection.Assembly]::LoadFrom($dll)
$catalog = $assembly.GetType('HutchVRisingTrainer.ItemCatalog', $true)
$flags = [Reflection.BindingFlags]'NonPublic, Static'
$all = $catalog.GetProperty('All', $flags).GetValue($null)
$swords = $catalog.GetMethod('Search', $flags).Invoke($null, @('sword'))

if ($all.Count -ne 1107) { throw "Expected 1107 catalog entries, found $($all.Count)." }
if ($swords.Count -lt 30) { throw "Sword search found only $($swords.Count) items." }
if (-not @($swords | Where-Object { $_.Name -eq 'Item_Weapon_Sword_T01_Bone' }).Count) {
    throw 'Bone sword is missing from sword search.'
}
Write-Output "Catalog search passed: $($all.Count) items, $($swords.Count) sword results."
