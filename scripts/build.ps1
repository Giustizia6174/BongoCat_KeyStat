<#
 Builds the release package: release/BongoKeyStats-<version>.zip
 Needs: .NET SDK 9, and the game's Managed folder for Unity reference assemblies:
   $env:BONGOCAT_MANAGED = 'D:\SteamLibrary\steamapps\common\BongoCat\BongoCat_Data\Managed'
#>
param([string]$Version = (Get-Content (Join-Path (Split-Path -Parent $PSScriptRoot) 'VERSION') -TotalCount 1).Trim())
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $env:BONGOCAT_MANAGED) { throw 'Set BONGOCAT_MANAGED to the game''s BongoCat_Data\Managed folder first.' }
Push-Location $root
try {
    & "$PSScriptRoot\gen-template.ps1"
    dotnet run --project Tests -c Release; if ($LASTEXITCODE) { throw 'unit tests failed' }
    dotnet build src -c Release; if ($LASTEXITCODE) { throw 'module build failed' }
    $name = "BongoKeyStats-$Version"
    $out = Join-Path $root "release\$name"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish Patcher -c Release -o $out; if ($LASTEXITCODE) { throw 'patcher publish failed' }
    Get-ChildItem $out -Include *.pdb, Mono.Cecil.Mdb.dll -Recurse | Remove-Item -Force
    Copy-Item src\bin\Release\net471\BongoKeyStats.dll $out
    Copy-Item package\* $out
    Copy-Item LICENSE, THIRD-PARTY-NOTICES.md $out
    $zip = Join-Path $root "release\$name.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path "$out\*" -DestinationPath $zip
    Write-Host "Built $zip"
} finally { Pop-Location }
