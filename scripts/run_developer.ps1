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
    Write-Host "Launching Engine.Server (developer mode)..."
    $server = Start-DotnetProcess (Join-Path $repoRoot "src/Engine.Server")
    Start-Sleep -Seconds 3

    Write-Host "Launching Engine.Client (Mission Control)..."
    $client = Start-DotnetProcess (Join-Path $repoRoot "src/Engine.Client")
    Start-Sleep -Seconds 4

    if ($shouldOpenBrowser) {
        Write-Host "Opening Mission Control and /devtools surfaces..."
        Open-Browser "https://localhost:7061/"
        Open-Browser "https://localhost:7061/devtools"
    }

    Write-Host "Press Ctrl+C to stop both hosts."
    Wait-Process -Id $server.Id, $client.Id
}
finally {
    Stop-ProcessSafe $client
    Stop-ProcessSafe $server
    Pop-Location
}
