param(
    [string]$ProjectPath,
    [string]$EditorPath,
    [string]$UrpVersion,
    [switch]$NoLaunch
)
$ErrorActionPreference = 'Stop'
# Works in Windows PowerShell 5.1 and in PowerShell 7 on Windows, macOS and Linux.
$onWindows = [Environment]::OSVersion.Platform -eq 'Win32NT'
$userHome = [Environment]::GetFolderPath('UserProfile')
if (!$ProjectPath) {
    if ($onWindows) { $ProjectPath = 'C:\UnityProjects\Bidwarss' } else { $ProjectPath = Join-Path $userHome 'UnityProjects/Bidwarss' }
}

function Find-UnityEditors {
    $found = @()
    if ($onWindows) {
        foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
            if ($root) { $found += @(Get-ChildItem (Join-Path $root 'Unity/Hub/Editor/6000.*/Editor/Unity.exe') -ErrorAction SilentlyContinue) }
        }
    } elseif (Test-Path '/Applications/Unity/Hub/Editor') {
        $found += @(Get-ChildItem '/Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity' -ErrorAction SilentlyContinue)
    } else {
        $found += @(Get-ChildItem (Join-Path $userHome 'Unity/Hub/Editor/6000.*/Editor/Unity') -ErrorAction SilentlyContinue)
    }
    return $found
}
function Get-EditorVersion([string]$path) {
    $m = [regex]::Match($path, '6000\.\d+\.\d+')
    if ($m.Success) { return [Version]$m.Value }
    return [Version]'0.0.0'
}
function Invoke-Unity([string]$logPath, [string[]]$unityArguments, [string]$failure) {
    $process = Start-Process $EditorPath -ArgumentList ($unityArguments + @('-logFile', ('"' + $logPath + '"'))) -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "$failure Log: $logPath" }
}

if (!$EditorPath) {
    $editors = @(Find-UnityEditors)
    if (!$editors.Count) { throw 'Unity 6 bulunamadi. -EditorPath ile Unity calistirilabilir dosyasinin yolunu belirt.' }
    $EditorPath = ($editors | Sort-Object { Get-EditorVersion $_.FullName } -Descending | Select-Object -First 1).FullName
}
if (!(Test-Path $EditorPath)) { throw 'Unity calistirilabilir dosyasi bulunamadi.' }
if (Test-Path $ProjectPath) { throw 'Hedef zaten var. Mevcut projeyi Install-Bidwarss.ps1 ile guncelle veya yeni bir klasor sec.' }
if (!$UrpVersion) {
    $series = [regex]::Match($EditorPath, '(6000\.\d+)\.\d+')
    if (!$series.Success) { throw 'Unity 6 gerekli; ozel kurulumda -UrpVersion belirt.' }
    $unitySeries = $series.Groups[1].Value
    $registry = Invoke-RestMethod 'https://packages.unity.com/com.unity.render-pipelines.universal'
    $compatible = @($registry.versions.PSObject.Properties | Where-Object { $_.Value.unity -eq $unitySeries -and $_.Name -match '^\d+\.\d+\.\d+$' } | Sort-Object { [Version]$_.Name } -Descending)
    if (!$compatible.Count) { throw 'Uyumlu URP bulunamadi. Editorune uygun -UrpVersion belirt.' }
    $UrpVersion = $compatible[0].Name
}
$parent = Split-Path $ProjectPath -Parent
New-Item $parent -ItemType Directory -Force | Out-Null

Invoke-Unity (Join-Path $parent 'bidwarss-create.log') @('-batchmode', '-quit', '-createProject', ('"' + $ProjectPath + '"')) 'Unity proje olusturamadi.'
$manifestPath = Join-Path $ProjectPath 'Packages/manifest.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.dependencies | Add-Member -NotePropertyName 'com.unity.render-pipelines.universal' -NotePropertyValue $UrpVersion -Force
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 30), (New-Object System.Text.UTF8Encoding($false)))
& (Join-Path $PSScriptRoot 'Install-Bidwarss.ps1') -ProjectPath $ProjectPath

# Separate pass: let Unity resolve the packages and compile the sources before any editor method runs.
# A compile or package error stops here with its own log instead of a confusing scene-builder failure.
Invoke-Unity (Join-Path $parent 'bidwarss-import.log') @('-batchmode', '-quit', '-projectPath', ('"' + $ProjectPath + '"')) 'Unity paketleri cozemedi veya script derlenemedi. Projeyi Unity ile acip Console hatalarina bak.'
Invoke-Unity (Join-Path $parent 'bidwarss-build-scene.log') @('-batchmode', '-quit', '-projectPath', ('"' + $ProjectPath + '"'), '-executeMethod', 'Bidwarss.Editor.DepoGameplayBuilder.Build') "Sahne olusturulamadi. Projeyi Unity'de acip Console'u kontrol et."
if (!$NoLaunch) { Start-Process $EditorPath -ArgumentList @('-projectPath', ('"' + $ProjectPath + '"')) }
Write-Host "Hazir: $ProjectPath"
