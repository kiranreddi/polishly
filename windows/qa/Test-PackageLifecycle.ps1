[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InitialBundle,
    [Parameter(Mandatory = $true)]
    [string]$UpdateBundle,
    [string]$PackageName = "Polishly.WindowsCompanion"
)

$ErrorActionPreference = "Stop"

function Get-PolishlyPackage {
    Get-AppxPackage | Where-Object Name -eq $PackageName
}

if (Get-PolishlyPackage) {
    throw "Polishly is already installed. Use a clean Windows test account."
}

Write-Host "Installing initial bundle..."
Add-AppxPackage $InitialBundle
$initial = Get-PolishlyPackage
if (-not $initial) { throw "Clean install failed." }

Write-Host "Updating..."
Add-AppxPackage $UpdateBundle
$updated = Get-PolishlyPackage
if (-not $updated -or $updated.Version -le $initial.Version) {
    throw "Update did not install a newer version."
}

Write-Host "Repairing package registration..."
if (Get-Command Repair-AppxPackage -ErrorAction SilentlyContinue) {
    Repair-AppxPackage -Package $updated.PackageFullName
} else {
    Add-AppxPackage -Register "$($updated.InstallLocation)\AppxManifest.xml" -DisableDevelopmentMode
}

Write-Host "Testing rollback..."
Add-AppxPackage $InitialBundle -ForceUpdateFromAnyVersion
$rolledBack = Get-PolishlyPackage
if (-not $rolledBack -or $rolledBack.Version -ne $initial.Version) {
    throw "Rollback did not restore the initial version."
}

Write-Host "Uninstalling..."
Remove-AppxPackage $rolledBack.PackageFullName
if (Get-PolishlyPackage) { throw "Uninstall failed." }

Write-Host "Install, update, repair, rollback, and uninstall all passed."
