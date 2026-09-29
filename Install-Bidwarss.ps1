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
foreach ($folder in @('Bidwarss','DepoLevel')) {
    $existing = Join-Path $project ('Assets/' + $folder)
    if (Test-Path $existing) { Copy-Item $existing (Join-Path $backup $folder) -Recurse }
}
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
foreach ($folder in @('Bidwarss','DepoLevel')) {
    $source = Join-Path $PSScriptRoot ('Assets/' + $folder)
    $target = Join-Path $project ('Assets/' + $folder)
    New-Item $target -ItemType Directory -Force | Out-Null
    foreach ($file in Get-ChildItem $source -File -Recurse -Force) {
        $relative = $file.FullName.Substring($source.Length).TrimStart([char]'\', [char]'/')
        $destination = Join-Path $target $relative
        New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
        # Preserve GUIDs of the interior the user already imported; existing scenes retain references.
        if ($file.Extension -eq '.meta' -and (Test-Path $destination)) { continue }
        Copy-Item $file.FullName $destination -Force
    }
    $folderMeta = Join-Path $project ('Assets/' + $folder + '.meta')
    if (!(Test-Path $folderMeta)) { Copy-Item (Join-Path $PSScriptRoot ('Assets/' + $folder + '.meta')) $folderMeta }
}
# ZIP imports can create Assets/Assets/DepoLevel alongside the installed copy.
# Keep imported art in place; quarantine only duplicate source files after the
# canonical installation has completed successfully. Nothing is deleted.
$duplicateScripts = Join-Path $project 'Assets/Assets/DepoLevel/Scripts'
$canonicalScripts = Join-Path $project 'Assets/DepoLevel/Scripts'
if (Test-Path $duplicateScripts) {
    foreach ($file in Get-ChildItem $duplicateScripts -Filter '*.cs' -File -Recurse) {
        $relative = $file.FullName.Substring($duplicateScripts.Length).TrimStart([char]'\', [char]'/')
        if (!(Test-Path (Join-Path $canonicalScripts $relative))) { continue }
        $destination = Join-Path $backup ('DuplicateDepoScripts/' + $relative)
        New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
        Move-Item $file.FullName $destination
        if (Test-Path ($file.FullName + '.meta')) {
            Move-Item ($file.FullName + '.meta') ($destination + '.meta')
        }
        Write-Host "Cift script yedeklendi: $relative"
    }
}
Write-Host "Kuruldu. Yedek: $backup"
Write-Host 'Unity: Bidwarss > Build Uploaded Depot (Co-op). Mevcut sahnelerin korunur.'
