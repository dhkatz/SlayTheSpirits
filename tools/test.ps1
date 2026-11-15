<#
.SYNOPSIS
    Launches two local Slay the Spire 2 instances (host + client) connected over LAN for multiplayer testing.

.DESCRIPTION
    Uses the game's built-in developer flags (see NMainMenu.CheckCommandLineArgs / NJoinFriendScreen.FastMpJoin):
      --fastmp host_standard   skip menus and host a standard lobby over ENet on 127.0.0.1:33771
      --fastmp join            skip menus and join 127.0.0.1:33771
      --clientId <id>          player net id; also selects a separate local profile folder
      --force-steam off        don't initialize Steam, so each instance uses its own user://default/<id>/ profile
      -wpos <x> <y>            windowed, positioned relative to the current screen

    Test profiles are separate from your real Steam save (fresh unlocks).

.EXAMPLE
    ./tools/test.ps1 -Build
    ./tools/test.ps1 -Clients 3
    ./tools/test.ps1 -Tavern     # every shop is the Tavern on all instances (dev console: room shop)
#>
[CmdletBinding()]
param(
    [string] $GameDir = 'E:\Games\Steam\steamapps\common\Slay the Spire 2',
    [ValidateRange(1, 3)] [int] $Clients = 1,
    [ValidateSet('host_standard', 'host_custom', 'host_daily', 'host_blitz', 'host_dread')] [string] $Mode = 'host_standard',
    [int] $ClientDelaySeconds = 8,
    [switch] $Build,
    [switch] $Tavern
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $GameDir 'SlayTheSpire2.exe'
if (-not (Test-Path $exe)) { throw "Game not found: $exe" }

if ($Build) {
    dotnet build (Join-Path $PSScriptRoot '..\mod\src\SlayTheSpirits') -nologo -v m
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

$extra = @()
if ($Tavern) { $extra += '--spirits-tavern' }

# Tile windows left-to-right / top-to-bottom.
$positions = @(@(0, 0), @(960, 0), @(0, 540), @(960, 540))

Write-Host "Starting host ($Mode)..."
Start-Process -FilePath $exe -WorkingDirectory $GameDir -ArgumentList (@(
    '--force-steam', 'off', '--fastmp', $Mode, '--clientId', '1',
    '-wpos', $positions[0][0], $positions[0][1]) + $extra)

# The client joins as soon as its main menu loads; give the host time to start listening.
Start-Sleep -Seconds $ClientDelaySeconds

for ($i = 1; $i -le $Clients; $i++) {
    $id = 1000 + $i
    Write-Host "Starting client $i (clientId $id)..."
    Start-Process -FilePath $exe -WorkingDirectory $GameDir -ArgumentList (@(
        '--force-steam', 'off', '--fastmp', 'join', '--clientId', $id,
        '-wpos', $positions[$i][0], $positions[$i][1]) + $extra)
}
