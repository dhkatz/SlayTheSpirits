<#
.SYNOPSIS
    Launches local Slay the Spire 2 instances (one host + clients) connected over LAN for multiplayer testing.

.DESCRIPTION
    Uses the game's built-in developer flags (see NMainMenu.CheckCommandLineArgs / NJoinFriendScreen.FastMpJoin):
      --fastmp host_standard   skip menus and host a standard lobby over ENet on 127.0.0.1:33771
      --fastmp join            skip menus and join 127.0.0.1:33771
      --clientId <id>          player net id; also selects a separate local profile folder
      --force-steam off        don't initialize Steam, so each instance uses its own user://default/<id>/ profile
      -wpos <x> <y>            windowed, positioned relative to the current screen

    Test profiles are separate from your real Steam save (fresh unlocks). Windows are tiled in a 2x2 grid over
    the primary screen.

    The game folder is found automatically (see tools/common.ps1): -GameDir, then $env:STS2_GAME_DIR, then
    Sts2GameDir in local.props, then your Steam libraries.

.EXAMPLE
    ./tools/multiplayer.ps1 -Build
    ./tools/multiplayer.ps1 -Clients 3
    ./tools/multiplayer.ps1 -Mode host_daily
#>
[CmdletBinding()]
param(
    [string] $GameDir,
    [ValidateRange(1, 3)] [int] $Clients = 1,
    [ValidateSet('host_standard', 'host_custom', 'host_daily', 'host_blitz', 'host_dread')] [string] $Mode = 'host_standard',
    [int] $ClientDelaySeconds = 8,
    [switch] $Build
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$GameDir = Resolve-Sts2GameDir $GameDir

if ($Build) {
    Invoke-ModBuild $GameDir
} elseif (Test-Sts2Running) {
    Write-Warning 'The game is already running; starting more instances.'
}

$positions = Get-WindowGrid
Write-Host "Game: $GameDir"

Write-Host "Starting host ($Mode)..."
Start-Sts2 $GameDir @(
    '--force-steam', 'off', '--fastmp', $Mode, '--clientId', '1',
    '-wpos', $positions[0][0], $positions[0][1])

# The client joins as soon as its main menu loads; give the host time to start listening.
Start-Sleep -Seconds $ClientDelaySeconds

for ($i = 1; $i -le $Clients; $i++) {
    $id = 1000 + $i
    Write-Host "Starting client $i (clientId $id)..."
    Start-Sts2 $GameDir @(
        '--force-steam', 'off', '--fastmp', 'join', '--clientId', $id,
        '-wpos', $positions[$i][0], $positions[$i][1])
}

Write-Host "Log: $(Get-Sts2LogPath)"
