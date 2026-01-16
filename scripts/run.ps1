param(
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item (Join-Path $PSScriptRoot "..")).FullName
$shouldOpenBrowser = -not $NoBrowser -and $env:NO_BROWSER -ne "1"

function Start-DotnetProcess {
    param([string]$ProjectPath)
    Start-Process "dotnet" -ArgumentList @("run", "--project", $ProjectPath) -WorkingDirectory $repoRoot -PassThru -NoNewWindow
}

function Stop-ProcessSafe {
    param($Process)
    if ($null -eq $Process) { return }
    if ($Process.HasExited) { return }
    try { $Process.CloseMainWindow() | Out-Null } catch { }
    Start-Sleep -Seconds 1
    if (-not $Process.HasExited) {
        try { $Process.Kill() } catch { }
    }
}

function Open-Browser {
    param([string]$Url)
    Start-Process "cmd.exe" -ArgumentList @("/c", "start", "", $Url) | Out-Null
}

Push-Location $repoRoot
try {
    Write-Host "Launching Engine.Server for end-user session..."
    $server = Start-DotnetProcess (Join-Path $repoRoot "src/Engine.Server")
    Start-Sleep -Seconds 3

    Write-Host "Launching Engine.Client (Mission Control UI)..."
    $client = Start-DotnetProcess (Join-Path $repoRoot "src/Engine.Client")
    Start-Sleep -Seconds 4

    if ($shouldOpenBrowser) {
        Write-Host "Opening Mission Control..."
        Open-Browser "https://localhost:7061/"
    }

    Write-Host "Press Ctrl+C to stop the hosts."
    Wait-Process -Id $server.Id, $client.Id
}
finally {
    Stop-ProcessSafe $client
    Stop-ProcessSafe $server
    Pop-Location
}
