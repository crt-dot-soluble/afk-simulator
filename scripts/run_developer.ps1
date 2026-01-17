param(
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item (Join-Path $PSScriptRoot ".." )).FullName
$shouldOpenBrowser = -not $NoBrowser -and $env:NO_BROWSER -ne "1"
$previousDeveloperMode = $env:DeveloperMode__AutoLogin
$env:DeveloperMode__AutoLogin = "true"

function Start-DotnetProcess {
    param(
        [string]$ProjectPath,
        [string]$LaunchProfile
    )

    $args = @("run", "--project", $ProjectPath)
    if ($LaunchProfile) {
        $args += @("--launch-profile", $LaunchProfile)
    }

    Start-Process "dotnet" -ArgumentList $args -WorkingDirectory $repoRoot -PassThru -NoNewWindow
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
    if ([string]::IsNullOrWhiteSpace($Url)) { return }
    Start-Process $Url | Out-Null
}

function Get-PortOwner {
    param([int]$Port)

    try {
        return Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop |
            Select-Object -First 1
    }
    catch {
        return $null
    }
}

function Ensure-PortAvailable {
    param(
        [int[]]$Ports,
        [string]$Label
    )

    foreach ($port in $Ports) {
        $connection = Get-PortOwner $port
        if ($null -eq $connection) {
            continue
        }

        $process = Get-CimInstance Win32_Process -Filter "ProcessId = $($connection.OwningProcess)" -ErrorAction SilentlyContinue
        $commandLine = ""
        if ($null -ne $process -and $null -ne $process.CommandLine) {
            $commandLine = $process.CommandLine
        }
        $ownsRepoProcess = ($commandLine.Length -gt 0) -and ($commandLine.IndexOf($repoRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0)

        if ($ownsRepoProcess) {
            Write-Warning "$Label port $port is busy (PID $($connection.OwningProcess)). Stopping orphaned host."
            Stop-Process -Id $connection.OwningProcess -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 1
        }
        else {
            throw "Port $port required for $Label is in use by PID $($connection.OwningProcess). Close that process or adjust launchSettings.json."
        }
    }
}

Push-Location $repoRoot
try {
    Ensure-PortAvailable @(7206, 5206) "Engine.Server"
    Write-Host "Launching Engine.Server (developer mode)..."
    $server = Start-DotnetProcess (Join-Path $repoRoot "src/Engine.Server") "https"
    Start-Sleep -Seconds 3

    Ensure-PortAvailable @(7061, 5269) "Engine.Client"
    Write-Host "Launching Engine.Client (Mission Control)..."
    $client = Start-DotnetProcess (Join-Path $repoRoot "src/Engine.Client") "https"
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
    if ($null -eq $previousDeveloperMode) {
        Remove-Item Env:DeveloperMode__AutoLogin -ErrorAction SilentlyContinue
    }
    else {
        $env:DeveloperMode__AutoLogin = $previousDeveloperMode
    }
    Pop-Location
}
