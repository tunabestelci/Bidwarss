param([Parameter(Mandatory=$true)][string]$ProjectPath)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path $ProjectPath).Path
$manifestPath = Join-Path $project 'Packages/manifest.json'
$settingsPath = Join-Path $project 'ProjectSettings/ProjectSettings.asset'
if (!(Test-Path $manifestPath) -or !(Test-Path $settingsPath)) { throw 'Gecerli Unity projesi sec.' }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if (!$manifest.dependencies.'com.unity.render-pipelines.universal') { throw 'Universal RP paketi gerekli. Yeni proje icin New-BidwarssProject.ps1 kullan.' }
$backup = Join-Path $project ('.bidwarss-backups/' + [Guid]::NewGuid().ToString('N'))
New-Item $backup -ItemType Directory -Force | Out-Null
Copy-Item $manifestPath (Join-Path $backup 'manifest.json')
Copy-Item $settingsPath (Join-Path $backup 'ProjectSettings.asset')
$source = Join-Path $PSScriptRoot 'Assets/Bidwarss'
$target = Join-Path $project 'Assets/Bidwarss'
if (Test-Path $target) { Copy-Item $target (Join-Path $backup 'Bidwarss') -Recurse }
$deps = Get-Content (Join-Path $PSScriptRoot 'Packages/bidwarss-dependencies.json') -Raw | ConvertFrom-Json
foreach ($property in $deps.PSObject.Properties) {
    $current = $manifest.dependencies.PSObject.Properties[$property.Name]
    if ($null -ne $current) {
        $installed = $null
        if (![Version]::TryParse($current.Value, [ref]$installed)) { throw "Ozel paket referansini elle kontrol et: $($property.Name)" }
        if ($installed -ge [Version]$property.Value) { continue }
    }
    $manifest.dependencies | Add-Member -NotePropertyName $property.Name -NotePropertyValue $property.Value -Force
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 30), $utf8)
$settings = Get-Content $settingsPath -Raw
$settings = [regex]::Replace($settings, '(?m)^(\s*activeInputHandler:)\s*\d+\s*$', '$1 2')
[IO.File]::WriteAllText($settingsPath, $settings, $utf8)
New-Item $target -ItemType Directory -Force | Out-Null
Get-ChildItem $source -Force | Copy-Item -Destination $target -Recurse -Force
Write-Host "Kuruldu. Yedek: $backup"
Write-Host 'Unity: Bidwarss > Create Gameplay Scene. Mevcut GeneratedV2 sahnen korunur.'
