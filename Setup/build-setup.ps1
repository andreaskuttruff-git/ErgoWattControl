# Builds Setup\out\ErgoWattControl-Setup.exe: publishes the app, fetches the current .NET 10 Desktop Runtime
# installer from Microsoft (checked against its published SHA-512) and builds the WiX bundle.
#
#   powershell -ExecutionPolicy Bypass -File Setup\build-setup.ps1 [-Version 1.0.0]
param([string]$Version = "1.0.0")
$ErrorActionPreference = "Stop"
$setup = $PSScriptRoot
$root = Split-Path $setup -Parent

# 1. The app (framework-dependent, win-x64)
$app = Join-Path $setup "out\app"
if (Test-Path $app) { Remove-Item $app -Recurse -Force }
dotnet publish (Join-Path $root "ErgoWattControl.csproj") -c Release -r win-x64 --self-contained false -o $app -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fehlgeschlagen" }

# 2. .NET 10 Desktop Runtime installer (latest 10.0 release)
$redist = Join-Path $setup "redist"
New-Item -ItemType Directory -Force $redist | Out-Null
$runtime = Join-Path $redist "windowsdesktop-runtime-win-x64.exe"
$releases = Invoke-RestMethod "https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json"
$file = $releases.releases[0].windowsdesktop.files | Where-Object { $_.rid -eq "win-x64" -and $_.name -like "*.exe" } | Select-Object -First 1
$have = if (Test-Path $runtime) { (Get-FileHash $runtime -Algorithm SHA512).Hash } else { "" }
if ($have -ne $file.hash.ToUpperInvariant()) {
    Write-Host "Lade $($file.url)"
    Invoke-WebRequest $file.url -OutFile $runtime
    if ((Get-FileHash $runtime -Algorithm SHA512).Hash -ne $file.hash.ToUpperInvariant()) { throw "Prüfsumme der .NET-Runtime stimmt nicht" }
}
Write-Host ".NET Desktop Runtime $($releases.releases[0].'release-version')"

# 3. MSI + bundle
dotnet build (Join-Path $setup "Bundle\ErgoWattControl.Bundle.wixproj") -c Release -p:Version=$Version -p:AppDir=$app -p:DotNetRuntime=$runtime -o (Join-Path $setup "out")
if ($LASTEXITCODE -ne 0) { throw "WiX-Build fehlgeschlagen" }
Get-Item (Join-Path $setup "out\ErgoWattControl-Setup.exe") | Select-Object Name, Length
