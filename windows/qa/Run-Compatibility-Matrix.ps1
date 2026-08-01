[CmdletBinding()]
param(
    [ValidateRange(1, 100)]
    [int]$Attempts = 20,
    [string]$OutputDirectory = "$PSScriptRoot\results"
)

$ErrorActionPreference = "Stop"
$applications = @(
    "Windows Notepad",
    "Microsoft Teams",
    "Outlook Classic",
    "New Outlook",
    "Microsoft Word",
    "Slack",
    "Chrome",
    "Edge",
    "Gmail in Chrome",
    "Gmail in Edge",
    "Visual Studio Code",
    "OneNote"
)

New-Item $OutputDirectory -ItemType Directory -Force | Out-Null
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$records = [System.Collections.Generic.List[object]]::new()

function Read-Boolean([string]$Prompt) {
    while ($true) {
        $answer = (Read-Host "$Prompt [y/n]").Trim().ToLowerInvariant()
        if ($answer -eq "y") { return $true }
        if ($answer -eq "n") { return $false }
    }
}

Write-Host "Polishly interactive compatibility qualification"
Write-Host "Use ordinary, non-sensitive sample text. Never test in password fields."
Write-Host "For every attempt: select text, invoke Polishly, review the diff, then Accept."

foreach ($application in $applications) {
    Write-Host "`n=== $application ==="
    Read-Host "Open the target editor and press Enter when ready"
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        Write-Host "`n$application attempt $attempt of $Attempts"
        $captured = Read-Boolean "Was the exact selection captured?"
        $replaced = Read-Boolean "Was the exact original field replaced correctly?"
        $wrongTarget = Read-Boolean "Did any text paste into a wrong window or field?"
        $clipboardCorruption = Read-Boolean "Was any pre-existing clipboard format lost?"
        $recoveryVisible = if ($captured -and $replaced) {
            $true
        } else {
            Read-Boolean "Was an explicit recovery/Copy state visible?"
        }
        $notes = Read-Host "Notes (optional)"

        $records.Add([pscustomobject]@{
            RunId = $runId
            TimestampUtc = [DateTime]::UtcNow.ToString("o")
            Machine = $env:COMPUTERNAME
            WindowsVersion = [Environment]::OSVersion.VersionString
            Application = $application
            Attempt = $attempt
            CaptureSuccess = $captured
            ReplacementSuccess = $replaced
            WrongTargetPaste = $wrongTarget
            ClipboardCorruption = $clipboardCorruption
            RecoveryVisible = $recoveryVisible
            Notes = $notes
        })
    }
}

$jsonPath = Join-Path $OutputDirectory "compatibility-$runId.json"
$records | ConvertTo-Json -Depth 4 | Set-Content $jsonPath -Encoding utf8

$summary = $records | Group-Object Application | ForEach-Object {
    $items = $_.Group
    [pscustomobject]@{
        Application = $_.Name
        Attempts = $items.Count
        CapturePercent = [Math]::Round(
            100 * ($items | Where-Object CaptureSuccess).Count / $items.Count, 1)
        ReplacementPercent = [Math]::Round(
            100 * ($items | Where-Object ReplacementSuccess).Count / $items.Count, 1)
        WrongTargetPastes = ($items | Where-Object WrongTargetPaste).Count
        ClipboardCorruptions = ($items | Where-Object ClipboardCorruption).Count
        RecoveryPercent = [Math]::Round(
            100 * ($items | Where-Object RecoveryVisible).Count / $items.Count, 1)
    }
}
$csvPath = Join-Path $OutputDirectory "compatibility-summary-$runId.csv"
$summary | Export-Csv $csvPath -NoTypeInformation
$summary | Format-Table -AutoSize
Write-Host "Raw results: $jsonPath"
Write-Host "Summary: $csvPath"
