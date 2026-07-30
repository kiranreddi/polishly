[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Publisher,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^https://')]
    [string]$BaseUri,

    [string]$CertificatePath,
    [string]$CertificatePassword,
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "$PSScriptRoot\..\artifacts"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path "$PSScriptRoot\..").Path
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
$stage = Join-Path $output "stage"
$publish = Join-Path $output "publish"
$bundleStage = Join-Path $output "bundle"

Remove-Item $stage, $publish, $bundleStage -Recurse -Force -ErrorAction SilentlyContinue
New-Item $stage, $publish, $bundleStage -ItemType Directory -Force | Out-Null

dotnet publish "$root\src\Polishly.App\Polishly.App.csproj" `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $publish `
    -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Copy-Item "$publish\*" $stage -Recurse -Force
Copy-Item "$root\src\Polishly.App\Assets" "$stage\Assets" -Recurse -Force

[xml]$manifest = Get-Content "$root\src\Polishly.App\Package.appxmanifest"
$manifest.Package.Identity.Version = $Version
$manifest.Package.Identity.Publisher = $Publisher
$manifest.Save((Join-Path $stage "AppxManifest.xml"))

$kitsRoot = (Get-ItemProperty `
    "HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots").KitsRoot10
$makeAppx = Get-ChildItem "$kitsRoot\bin\*\x64\makeappx.exe" |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $makeAppx) { throw "MakeAppx.exe was not found in the Windows SDK." }

$packageName = "Polishly-$Version-x64.msix"
$packagePath = Join-Path $output $packageName
& $makeAppx.FullName pack /o /d $stage /p $packagePath
if ($LASTEXITCODE -ne 0) { throw "MakeAppx package creation failed." }

Copy-Item $packagePath (Join-Path $bundleStage $packageName)
$bundleName = "Polishly-$Version-x64.msixbundle"
$bundlePath = Join-Path $output $bundleName
& $makeAppx.FullName bundle /o /d $bundleStage /p $bundlePath
if ($LASTEXITCODE -ne 0) { throw "MakeAppx bundle creation failed." }

if ($CertificatePath) {
    $signTool = Join-Path $makeAppx.Directory.FullName "signtool.exe"
    if (-not (Test-Path $signTool)) { throw "SignTool.exe was not found." }
    & $signTool sign /fd SHA256 /f $CertificatePath /p $CertificatePassword $packagePath
    if ($LASTEXITCODE -ne 0) { throw "Package signing failed." }
    & $signTool sign /fd SHA256 /f $CertificatePath /p $CertificatePassword $bundlePath
    if ($LASTEXITCODE -ne 0) { throw "Bundle signing failed." }
}

$template = Get-Content "$PSScriptRoot\Polishly.appinstaller.template" -Raw
$appInstaller = $template.Replace("{{VERSION}}", $Version)
$appInstaller = $appInstaller.Replace("{{PUBLISHER}}", $Publisher)
$appInstaller = $appInstaller.Replace("{{BASE_URI}}", $BaseUri.TrimEnd('/'))
Set-Content (Join-Path $output "Polishly.appinstaller") $appInstaller -Encoding utf8

Get-FileHash $packagePath, $bundlePath -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_.Path))" } |
    Set-Content (Join-Path $output "SHA256SUMS.txt") -Encoding ascii

Write-Host "Windows release artifacts created in $output"
