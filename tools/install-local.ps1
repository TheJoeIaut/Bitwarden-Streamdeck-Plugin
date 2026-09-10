<#
.SYNOPSIS
    Builds the plugin and installs it into the local Stream Deck.

.DESCRIPTION
    Publishes a Windows specific, framework dependent build and copies it over
    %APPDATA%\Elgato\StreamDeck\Plugins\com.thejoeiaut.bitwarden.sdPlugin.

    Stream Deck holds the plugin executable open, so it has to be stopped first. It often
    runs at a higher integrity level than an ordinary shell, in which case stopping it needs
    an elevated prompt - the script says so rather than failing obscurely.

    A plain 'dotnet build' is deliberately not used: since the plugin gained Linux and macOS
    targets its output carries every platform's SkiaSharp natives and runs to ~180 MB.
    Publishing for win-x64 keeps it at ~16 MB.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/install-local.ps1
#>
[CmdletBinding()]
param(
    # Skip the build and install whatever is already in the publish folder.
    [switch] $SkipBuild,

    # Do not start Stream Deck again afterwards.
    [switch] $NoRelaunch,

    # How long to wait for Stream Deck to be quit by hand when it cannot be stopped here.
    [int] $WaitMinutes = 10
)

$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$project    = Join-Path $repoRoot 'BitwardenCLI\BitwardenStreamdeckPlugin.csproj'
$stage      = Join-Path $repoRoot 'artifacts\com.thejoeiaut.bitwarden.sdPlugin'
$pluginName = 'com.thejoeiaut.bitwarden.sdPlugin'
$dest       = Join-Path $env:APPDATA "Elgato\StreamDeck\Plugins\$pluginName"
$streamDeck = Join-Path $env:ProgramFiles 'Elgato\StreamDeck\StreamDeck.exe'

if (-not $SkipBuild) {
    Write-Host 'Publishing win-x64...' -ForegroundColor Cyan
    if (Test-Path $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    dotnet publish $project -c Release -r win-x64 --self-contained false -o $stage | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
}

if (-not (Test-Path (Join-Path $stage 'com.thejoeiaut.bitwarden.exe'))) {
    throw "No build found at $stage. Run without -SkipBuild."
}

$wasRunning = [bool] (Get-Process StreamDeck -ErrorAction SilentlyContinue)

if ($wasRunning) {
    Write-Host 'Stopping Stream Deck...' -ForegroundColor Cyan
    try {
        Get-Process StreamDeck -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction Stop
    }
    catch {
        # Stream Deck usually runs at a higher integrity level than an ordinary shell, so
        # this only works from an elevated one. Waiting beats prompting: it behaves the same
        # whether a person is watching or a script is driving.
        Write-Warning 'Cannot stop Stream Deck from this shell - it runs elevated.'
        Write-Warning "Quit it from its tray icon; waiting up to $WaitMinutes minute(s)..."

        foreach ($i in 1..($WaitMinutes * 30)) {
            if (-not (Get-Process StreamDeck -ErrorAction SilentlyContinue)) { break }
            Start-Sleep -Seconds 2
        }
    }

    # The plugin is a child process and usually goes with it.
    foreach ($i in 1..15) {
        if (-not (Get-Process -Name 'com.thejoeiaut.bitwarden' -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Seconds 1
    }
    Get-Process -Name 'com.thejoeiaut.bitwarden' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

if (Get-Process StreamDeck -ErrorAction SilentlyContinue) {
    throw 'Stream Deck is still running; its files are locked. Quit it and run this again.'
}

Write-Host "Installing to $dest" -ForegroundColor Cyan
if (Test-Path $dest) { Remove-Item -LiteralPath $dest -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item -Path (Join-Path $stage '*') -Destination $dest -Recurse -Force

$size = (Get-ChildItem -Recurse -File $dest | Measure-Object Length -Sum).Sum / 1MB
Write-Host ('Installed {0:N1} MB' -f $size) -ForegroundColor Green

if ($NoRelaunch -or -not $wasRunning) {
    Write-Host 'Start Stream Deck when you are ready.'
    return
}

Write-Host 'Starting Stream Deck...' -ForegroundColor Cyan
$log = Join-Path $dest 'pluginlog.log'
Remove-Item $log -ErrorAction SilentlyContinue
Start-Process $streamDeck

foreach ($i in 1..45) {
    Start-Sleep -Seconds 2
    if (Test-Path $log) { break }
}
Start-Sleep -Seconds 4

if (Test-Path $log) {
    Write-Host '--- plugin log ---' -ForegroundColor Cyan
    Get-Content $log -Tail 8
}
else {
    Write-Warning 'The plugin has not logged anything yet. Open a Bitwarden action to wake it.'
}
