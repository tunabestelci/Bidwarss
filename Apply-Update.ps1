# Bu daha onceki gonderilen Assets guncellemesini Unity projene uygular.
# Kullanim (Unity KAPALI iken):  powershell -ExecutionPolicy Bypass -File .\Apply-Update.ps1
# Baska proje yolu icin:        ... -File .\Apply-Update.ps1 -Project "D:\Yol\Bidwarss"
param([string]$Project = "C:\UnityProjects\Bidwarss", [switch]$Force)
$ErrorActionPreference = "Stop"
# Unity bu projeyi acik tutuyorsa Temp\UnityLockfile kilitli kalir (Unity Hub vb. buna takilmaz).
$lock = Join-Path $Project "Temp\UnityLockfile"
if (-not $Force -and (Test-Path $lock)) {
    try { $fs = [System.IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $fs.Close() }
    catch { Write-Host "Bu proje Unity'de hala acik. Unity'yi kapat ya da -Force ile calistir." -ForegroundColor Red; exit 1 }
}
$src = Join-Path $PSScriptRoot "Assets"
if (-not (Test-Path "$Project\Assets")) { Write-Host "Proje bulunamadi: $Project\Assets" -ForegroundColor Red; exit 1 }
$backup = Join-Path (Split-Path $Project -Parent) ("Bidwarss-yedek-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$count = 0
Get-ChildItem $src -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($src.Length + 1)
    $dst = Join-Path "$Project\Assets" $rel
    if (Test-Path $dst) {
        $bk = Join-Path $backup $rel
        New-Item -ItemType Directory -Force -Path (Split-Path $bk -Parent) | Out-Null
        Copy-Item $dst $bk -Force
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
    Copy-Item $_.FullName $dst -Force
    $count++
}
Write-Host "$count dosya kopyalandi. Eski halleri: $backup" -ForegroundColor Green
Write-Host "Simdi Unity yi ac, derlenmesini bekle, sonra SIRAYLA: Tools > Bidwars > Loot > 1) Esya katalogunu kur, sonra Tools > Bidwars > Loot > 2) Kasalari depoya yerlestir."
