param([string]$ProjectPath='C:\UnityProjects\Bidwarss', [string]$EditorPath, [string]$UrpVersion)
$ErrorActionPreference='Stop'
if (!$EditorPath) {
    $editors = @(Get-ChildItem "$env:ProgramFiles\Unity\Hub\Editor\6000.*\Editor\Unity.exe" -ErrorAction SilentlyContinue)
    if (!$editors.Count) { throw 'Unity 6 bulunamadi. -EditorPath ile Unity.exe yolunu belirt.' }
    $EditorPath = ($editors | Sort-Object { [Version]($_.Directory.Parent.Name -replace '[abfp].*$','') } -Descending | Select-Object -First 1).FullName
}
if (!(Test-Path $EditorPath)) { throw 'Unity.exe bulunamadi.' }
if (Test-Path $ProjectPath) { throw 'Hedef zaten var. Mevcut projeyi Install-Bidwarss.ps1 ile guncelle veya yeni bir klasor sec.' }
$versionName = Split-Path (Split-Path (Split-Path $EditorPath -Parent) -Parent) -Leaf
if (!$UrpVersion) {
    if ($versionName -notmatch '^(6000\.\d+)') { throw 'Unity 6 gerekli; ozel kurulumda -UrpVersion belirt.' }
    $unitySeries=$Matches[1]
    $registry=Invoke-RestMethod 'https://packages.unity.com/com.unity.render-pipelines.universal'
    $compatible=@($registry.versions.PSObject.Properties | Where-Object { $_.Value.unity -eq $unitySeries -and $_.Name -match '^\d+\.\d+\.\d+$' } | Sort-Object { [Version]$_.Name } -Descending)
    if (!$compatible.Count) { throw 'Uyumlu URP bulunamadi. Editorune uygun -UrpVersion belirt.' }
    $UrpVersion=$compatible[0].Name
}
$parent=Split-Path $ProjectPath -Parent
New-Item $parent -ItemType Directory -Force | Out-Null
$log=Join-Path $parent 'bidwarss-create.log'
$process = Start-Process $EditorPath -ArgumentList @("-batchmode", "-quit", "-createProject", ('"'+$ProjectPath+'"'), "-logFile", ('"'+$log+'"')) -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Unity proje olusturamadi. Log: $log" }
$path=Join-Path $ProjectPath 'Packages/manifest.json'
$manifest=Get-Content $path -Raw | ConvertFrom-Json
$manifest.dependencies | Add-Member -NotePropertyName 'com.unity.render-pipelines.universal' -NotePropertyValue $UrpVersion -Force
[IO.File]::WriteAllText($path,($manifest | ConvertTo-Json -Depth 30),(New-Object System.Text.UTF8Encoding($false)))
& (Join-Path $PSScriptRoot 'Install-Bidwarss.ps1') -ProjectPath $ProjectPath
$log=Join-Path $parent 'bidwarss-build-scene.log'
$process = Start-Process $EditorPath -ArgumentList @("-batchmode", "-quit", "-projectPath", ('"'+$ProjectPath+'"'), "-executeMethod", "Bidwarss.Editor.DepoGameplayBuilder.Build", "-logFile", ('"'+$log+'"')) -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Sahne olusturulamadi. Log: $log. Projeyi Unity'de acip Console'u kontrol et." }
Start-Process $EditorPath -ArgumentList @('-projectPath', ('"'+$ProjectPath+'"'))
