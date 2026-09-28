param([Parameter(Mandatory=$true)][string]$ProjectPath)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path $ProjectPath).Path
$manifestPath = Join-Path $project 'Packages/manifest.json'
$settingsPath = Join-Path $project 'ProjectSettings/ProjectSettings.asset'
$target = Join-Path $project 'Assets/Bidwarss'
if (!(Test-Path $manifestPath) -or !(Test-Path $settingsPath)) {
    throw 'Once Unity Hub ile yeni Universal 3D (URP) projesi olusturup bir kez ac ve kapat.'
}
if (Test-Path $target) { throw 'Assets/Bidwarss zaten var. Bu kurulum mevcut dosyalarin uzerine yazmaz.' }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if (!$manifest.dependencies.'com.unity.render-pipelines.universal') { throw 'Bu prototip Universal 3D (URP) projesi gerektirir.' }
$deps = Get-Content (Join-Path $PSScriptRoot 'Packages/bidwarss-dependencies.json') -Raw | ConvertFrom-Json
foreach ($property in $deps.PSObject.Properties) {
    $current = $manifest.dependencies.PSObject.Properties[$property.Name]
    if ($null -ne $current -and $current.Value -ne $property.Value) {
        throw ('Paket surumu farkli: ' + $property.Name + '. Bu baslangic paketini yeni, bos URP projesine kur.')
    }
}
$backup = $manifestPath + '.bidwarss-backup'
if (Test-Path $backup) { throw 'Onceki manifest yedegi var; kurulum durduruldu.' }
$settingsBackup = $settingsPath + '.bidwarss-backup'
if (Test-Path $settingsBackup) { throw 'Onceki Player Settings yedegi var; kurulum durduruldu.' }
foreach ($property in $deps.PSObject.Properties) {
    $manifest.dependencies | Add-Member -NotePropertyName $property.Name -NotePropertyValue $property.Value -Force
}
Copy-Item $manifestPath $backup
Copy-Item $settingsPath $settingsBackup
$utf8 = New-Object System.Text.UTF8Encoding($false)
try {
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 30), $utf8)
    $settings = Get-Content $settingsPath -Raw
    $settings = [regex]::Replace($settings, '(?m)^(\s*activeInputHandler:)\s*\d+\s*$', '$1 2')
    [IO.File]::WriteAllText($settingsPath, $settings, $utf8)
    Copy-Item (Join-Path $PSScriptRoot 'Assets/Bidwarss') $target -Recurse
} catch {
    Copy-Item $backup $manifestPath -Force
    Copy-Item $settingsBackup $settingsPath -Force
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    throw
}
Write-Host 'Kuruldu. Unity projeyi acsin ve paketleri indirsin. Ardindan Bidwarss > Create Prototype Scene.'
Write-Host 'Player > Active Input Handling = Both oldugunu kontrol et. Gerekirse editoru yeniden baslat.'
