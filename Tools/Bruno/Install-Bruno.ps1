param([Parameter(Mandatory=$true)][string]$ProjectPath)
$ErrorActionPreference='Stop'
$project=(Resolve-Path $ProjectPath).Path
$source=$PSScriptRoot
if (!(Test-Path (Join-Path $source 'Assets/Bidwarss'))) { $source=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path }
if (!(Test-Path (Join-Path $project 'Assets/Bidwarss/Scripts/Bidwarss.Runtime.asmdef'))) { throw 'Mevcut Bidwarss Unity proje klasorunu secin.' }
$baseline=@{
    'Assets/Bidwarss/Characters.meta'=@('FFF0661A1D8F448728F05E4FB85CA7DD0ABD805BA09BD6DDC17F7B3715FCF3F0')
    'Assets/Bidwarss/Characters/Bruno.meta'=@('97F26265F19AFCA9F329DA85ABDA15821D1CFB18718AEFD40D8E3DF65EDED0BB')
    'Assets/Bidwarss/Characters/Bruno/Resources.meta'=@('DAD2416E9F0DA5471F4BD65FCDB9EC6D5C35717C089F07CAD8FD62F79384A678')
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno.meta'=@('16B41952C536F7F1902DEC594007AE2F48A00F15172352DF4D066129640DECA8')
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/Bruno.fbx'=@('A1C47A6B1578F62E0065D0E854BA9FD820565153A10084FC9472BCD2F76E6AAD')
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/Bruno.fbx.meta'=@('402303CC3EFF498E38FA0359494DAC68F9266DE219BCE787B3E4F07357659A85')
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/BrunoHands.fbx'=@('56D06B01C68575201F8EC56B2FEF4A14D1D962110CD6517A91B3984146DD52E0')
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/BrunoHands.fbx.meta'=@('A8DD80FEAE3A29D4CED6DBA49516C4C275CE1AC3744DC699FA04DBF8FBA72184')
    'Assets/Bidwarss/Characters/Bruno/Shaders.meta'=@('B7620FA5461F36AE66F1D5FB66B5FEAB8F817B550CF2B68EFC34D3D240CE178D')
    'Assets/Bidwarss/Characters/Bruno/Shaders/BrunoCel.shader'=@('DF28D65471CBC3E89CBE602CCC9DE1900336E4700A9FBCED6C8268FB97E9F339')
    'Assets/Bidwarss/Characters/Bruno/Shaders/BrunoCel.shader.meta'=@('3E0C1C25B3649806D8AB31FC906D3B3BE7ECC5C248802BEC3423A15F6573D893')
    'Assets/Bidwarss/Editor/Bruno.meta'=@('7E17A2E3730CF4185B55B7956890E48696087F13BE343B22F713BBD973CDA2AF')
    'Assets/Bidwarss/Editor/Bruno/BrunoImporter.cs'=@('19179AFA8B62BC86E033F8D8645DC99E6B7D707FD45D1C52913A350A6C130AF5')
    'Assets/Bidwarss/Editor/Bruno/BrunoImporter.cs.meta'=@('47C09DD02A9FADB69862C274C0DA84EEA0F78E4823B81A55FB7572BADD05444D')
    'Assets/Bidwarss/Editor/Bruno/BrunoSetup.cs'=@('60B0AF3E32F691B1B8CC29860F6B0EDE7F4166947873A0DFA8EC1C35C5F2F76F')
    'Assets/Bidwarss/Editor/Bruno/BrunoSetup.cs.meta'=@('FF7FB71B8529E7554AA56D2AE04243C2DE5E4FF2EB30BE2A86A0DC8E73490EF5')
    'Assets/Bidwarss/Scripts/Core/ItemState.cs'=@('38E3C07BF9479143B5B3A4FF5AE5781DC7B24C5CE50309AF076E0933C9C3825B')
    'Assets/Bidwarss/Scripts/Networking/SessionMenu.cs'=@('83482ED5E1793BC7DD59C63DCDC3CB7BA66826DE330618F7FF9F9FB0DC184FB8')
    'Assets/Bidwarss/Scripts/Networking/WarehousePlayer.cs'=@('0F021CF33A5944C85B39F9C73CFBCA841650A9D7FEB5FA57AE58C9FB4632E63E')
    'Assets/Bidwarss/Scripts/Presentation/Bruno.meta'=@('A46542EB6C55B47B1D918846B551061D40CA247C757A7B6F5C43EF49F6573E24')
    'Assets/Bidwarss/Scripts/Presentation/Bruno/BrunoMotion.cs'=@('9378A4B96344B669F8950234E3EB24B9BD32F483F1B178A686B9943298CF7405')
    'Assets/Bidwarss/Scripts/Presentation/Bruno/BrunoMotion.cs.meta'=@('F41537ABD20F304F64EAC2C4CD954DD5A2F7A84437ED716029B008174B42A677')
    'Assets/Bidwarss/Scripts/Presentation/Bruno/BrunoPreview.cs'=@('A56A0DFEC1F8A1538DA77BEA1D2F222CEDBA6EC730485168BDA9FB5065338F17')
    'Assets/Bidwarss/Scripts/Presentation/Bruno/BrunoPreview.cs.meta'=@('47D860AEA5625E2CBBE2D4B04E5695C77F5A041461F8B8A4098B560CBA4B1B54')
    'Assets/Bidwarss/Scripts/Presentation/ItemVisual.cs'=@('872C189A15ED71516ABDAC990BC1261C0E9E5DE4D0EE9E137EE0C99712FB315E')
    'Assets/Bidwarss/Scripts/Presentation/WarehouseAvatar.cs'=@('709D1F6407E792BB9133BB9689FBC4D4F891C115DAEA13CB537DF291CBE7DF42','8883DDE30C2D7141E1B3405F476000DA728CAAFCF1AB7F8EC701C48CA0B96D5C')
    'Assets/Bidwarss/Scripts/Presentation/WarehouseHands.cs'=@('4C6254C7A21ECEE1C8A63ACEF13FD08F329E2935F258F34B11724AC526A28E79','CFDA5D14F767FBF4FB68661017CCFB3863147587442FF54C8E4937AD1FAD1BDD')
    'Assets/Bidwarss/Scripts/World/WarehouseWorld.cs'=@('9AC808BB4EF224EF3F901A3F9E80BE4412088E54E50927CA2CE197F23E536C90')
}
$paths=@(
    'Assets/Bidwarss/Characters.meta',
    'Assets/Bidwarss/Characters/Bruno.meta',
    'Assets/Bidwarss/Characters/Bruno/Resources.meta',
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno.meta',
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/Bruno.fbx',
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/Bruno.fbx.meta',
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/BrunoBoxCutter.fbx',
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/BrunoBoxCutter.fbx.meta',
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/BrunoHands.fbx',
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/BrunoHands.fbx.meta',
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/BrunoPryBar.fbx',
    'Assets/Bidwarss/Characters/Bruno/Resources/Bruno/BrunoPryBar.fbx.meta',
    'Assets/Bidwarss/Characters/Bruno/Shaders.meta',
    'Assets/Bidwarss/Characters/Bruno/Shaders/BrunoCel.shader',
    'Assets/Bidwarss/Characters/Bruno/Shaders/BrunoCel.shader.meta',
    'Assets/Bidwarss/Domain/CarryLayout.cs',
    'Assets/Bidwarss/Domain/CarryLayout.cs.meta',
    'Assets/Bidwarss/Editor/Bruno.meta',
    'Assets/Bidwarss/Editor/Bruno/BrunoImporter.cs',
    'Assets/Bidwarss/Editor/Bruno/BrunoImporter.cs.meta',
    'Assets/Bidwarss/Editor/Bruno/BrunoSetup.cs',
    'Assets/Bidwarss/Editor/Bruno/BrunoSetup.cs.meta',
    'Assets/Bidwarss/Scripts/Core/ItemState.cs',
    'Assets/Bidwarss/Scripts/Networking/SessionMenu.cs',
    'Assets/Bidwarss/Scripts/Networking/WarehousePlayer.cs',
    'Assets/Bidwarss/Scripts/Presentation/Bruno.meta',
    'Assets/Bidwarss/Scripts/Presentation/Bruno/BrunoArmIK.cs',
    'Assets/Bidwarss/Scripts/Presentation/Bruno/BrunoArmIK.cs.meta',
    'Assets/Bidwarss/Scripts/Presentation/Bruno/BrunoMotion.cs',
    'Assets/Bidwarss/Scripts/Presentation/Bruno/BrunoMotion.cs.meta',
    'Assets/Bidwarss/Scripts/Presentation/Bruno/BrunoPreview.cs',
    'Assets/Bidwarss/Scripts/Presentation/Bruno/BrunoPreview.cs.meta',
    'Assets/Bidwarss/Scripts/Presentation/Bruno/WarehouseGripRig.cs',
    'Assets/Bidwarss/Scripts/Presentation/Bruno/WarehouseGripRig.cs.meta',
    'Assets/Bidwarss/Scripts/Presentation/ItemVisual.cs',
    'Assets/Bidwarss/Scripts/Presentation/WarehouseAvatar.cs',
    'Assets/Bidwarss/Scripts/Presentation/WarehouseHands.cs',
    'Assets/Bidwarss/Scripts/World/CrateOpeningProfile.cs',
    'Assets/Bidwarss/Scripts/World/CrateOpeningProfile.cs.meta',
    'Assets/Bidwarss/Scripts/World/WarehouseWorld.cs'
)
$files=$paths | ForEach-Object { Get-Item (Join-Path $source $_) }
# Preflight all files before changing any of them. Preserve existing Unity GUIDs.
foreach($file in $files) {
    $relative=$file.FullName.Substring($source.Length).TrimStart([char]'\',[char]'/').Replace('\','/')
    $target=Join-Path $project $relative
    if ((Test-Path $target) -and $file.Extension -ne '.meta') {
        $current=(Get-FileHash $target -Algorithm SHA256).Hash
        $incoming=(Get-FileHash $file.FullName -Algorithm SHA256).Hash
        if ($current -ne $incoming -and $current -notin $baseline[$relative]) {
            throw "Yerel degisiklik bulundu: $relative. Dosyalar degistirilmedi. Patch'i birlestirin veya once yedeginizi alin."
        }
    }
}
$backup=Join-Path $project ('.bidwarss-backups/bruno-'+[Guid]::NewGuid().ToString('N'))
foreach($file in $files) {
    $relative=$file.FullName.Substring($source.Length).TrimStart([char]'\',[char]'/')
    $target=Join-Path $project $relative
    if ((Test-Path $target) -and $file.Extension -eq '.meta') { continue }
    if (Test-Path $target) {
        $saved=Join-Path $backup $relative
        New-Item (Split-Path $saved -Parent) -ItemType Directory -Force | Out-Null
        Copy-Item $target $saved
    }
    New-Item (Split-Path $target -Parent) -ItemType Directory -Force | Out-Null
    Copy-Item $file.FullName $target -Force
}
Write-Host 'Bruno kaynaklari kuruldu. Unity import bitince Bidwarss > Bruno > Open Character Preview.'
Write-Host "Yedek: $backup"
