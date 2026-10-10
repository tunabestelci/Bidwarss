param([Parameter(Mandatory=$true)][string]$ProjectPath,[switch]$QuarantineUnknown)
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
        # Accept 2.13.3 and prereleases such as 2.14.0-pre.1; anything else (file:, git URL) needs a human.
        $match = [regex]::Match([string]$current.Value, '^(\d+\.\d+\.\d+)(-.+)?$')
        if (!$match.Success) { throw "Ozel paket referansini elle kontrol et: $($property.Name)" }
        $installedVersion = [Version]$match.Groups[1].Value
        $required = [Version]$property.Value
        $isPrerelease = $match.Groups[2].Success
        # A prerelease of the required version is older than the release, so it is upgraded.
        if ($installedVersion -gt $required -or ($installedVersion -eq $required -and !$isPrerelease)) { continue }
    }
    $manifest.dependencies | Add-Member -NotePropertyName $property.Name -NotePropertyValue $property.Value -Force
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 30), $utf8)
$settings = Get-Content $settingsPath -Raw
$settings = [regex]::Replace($settings, '(?m)^(\s*activeInputHandler:)\s*\d+\s*$', '$1 2')
[IO.File]::WriteAllText($settingsPath, $settings, $utf8)
# Project-relative paths copied by this run. Compared with the previous run to find sources that were
# deleted or renamed upstream, which would otherwise linger and cause duplicate-type compile errors.
$installedFiles = New-Object System.Collections.Generic.List[string]
foreach ($folder in @('Bidwarss','DepoLevel')) {
    $source = Join-Path $PSScriptRoot ('Assets/' + $folder)
    $target = Join-Path $project ('Assets/' + $folder)
    New-Item $target -ItemType Directory -Force | Out-Null
    foreach ($file in Get-ChildItem $source -File -Recurse -Force) {
        $relative = $file.FullName.Substring($source.Length).TrimStart([char]'\', [char]'/')
        $destination = Join-Path $target $relative
        New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
        # Preserve GUIDs of the interior the user already imported; existing scenes retain references.
        $installedFiles.Add('Assets/' + $folder + '/' + ($relative -replace '\\', '/'))
        if ($file.Extension -eq '.meta' -and (Test-Path $destination)) { continue }
        Copy-Item $file.FullName $destination -Force
    }
    $folderMeta = Join-Path $project ('Assets/' + $folder + '.meta')
    if (!(Test-Path $folderMeta)) { Copy-Item (Join-Path $PSScriptRoot ('Assets/' + $folder + '.meta')) $folderMeta }
}
$installedList = Join-Path $project '.bidwarss-installed.txt'
if (Test-Path $installedList) {
    $current = @{}
    foreach ($entry in $installedFiles) { $current[$entry] = $true }
    foreach ($old in Get-Content $installedList) {
        if ([string]::IsNullOrWhiteSpace($old) -or $current.ContainsKey($old)) { continue }
        # Only ever touch files this installer itself placed under its two source folders.
        if ($old -notmatch '^Assets/(Bidwarss|DepoLevel)/' -or $old -match '\.\.') { continue }
        $stale = Join-Path $project $old
        if (!(Test-Path $stale -PathType Leaf)) { continue }
        $destination = Join-Path $backup ('Removed/' + $old)
        New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
        Move-Item $stale $destination
        Write-Host "Kaynakta artik olmayan dosya yedege tasindi: $old"
    }
}
[IO.File]::WriteAllLines($installedList, $installedFiles.ToArray(), $utf8)
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
# Scripts that sit in Assets/Bidwarss but are not part of this repository (left over from another copy of the game)
# usually reference types that no longer exist and break the whole compile. With -QuarantineUnknown they are moved,
# together with their .meta files, into the backup folder. Nothing is deleted.
if ($QuarantineUnknown) {
    $known = @{}
    foreach ($entry in $installedFiles) { $known[$entry] = $true }
    $root = Join-Path $project 'Assets/Bidwarss'
    foreach ($file in Get-ChildItem $root -Filter '*.cs' -File -Recurse -Force) {
        $relative = 'Assets/Bidwarss/' + ($file.FullName.Substring($root.Length).TrimStart([char]'\', [char]'/') -replace '\\', '/')
        if ($known.ContainsKey($relative)) { continue }
        $destination = Join-Path $backup ('Unknown/' + $relative)
        New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
        Move-Item $file.FullName $destination
        if (Test-Path ($file.FullName + '.meta')) { Move-Item ($file.FullName + '.meta') ($destination + '.meta') }
        Write-Host "Repoda olmayan script yedege tasindi: $relative"
    }
}
Write-Host "Kuruldu. Yedek: $backup"
Write-Host 'Unity: Bidwarss > Build Uploaded Depot (Co-op). Mevcut sahnelerin korunur.'
