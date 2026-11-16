<#
.SYNOPSIS
    Launches one Slay the Spire 2 instance with the mod, optionally rebuilding it first.

.DESCRIPTION
    By default the game starts without Steam on a local test profile (user://default/<Profile>/), so your real
    save is untouched; -Steam uses your normal Steam profile instead.

    The game folder is found automatically (see tools/common.ps1): -GameDir, then $env:STS2_GAME_DIR, then
    Sts2GameDir in local.props, then your Steam libraries.

    The build skips deploying while the game is running, so -Build closes running instances first.

.EXAMPLE
    ./tools/launch.ps1 -Build -Gallery    # rebuild, then open the SpireKit controls gallery (F8 toggles it)
    ./tools/launch.ps1 -Steam             # your real profile
    ./tools/launch.ps1 -Tavern            # every shop is the Tavern (dev console: room shop)
    ./tools/launch.ps1 -Profile 2         # another test profile
    ./tools/launch.ps1 -OpenSettings Spirits   # straight to a mod's Settings tab
    ./tools/launch.ps1 -DumpScenes res://scenes/screens/settings_screen.tscn   # node tree -> <mod storage>/dumps/
#>
[CmdletBinding()]
param(
    [string] $GameDir,
    [switch] $Build,
    [switch] $Gallery,
    [switch] $Tavern,
    [switch] $Steam,
    [int] $Profile = 1,
    [string[]] $DumpScenes,
    [string] $OpenSettings,
    [int] $Scroll,
    [switch] $NoWindowPosition
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$GameDir = Resolve-Sts2GameDir $GameDir

if ($Build) {
    Invoke-ModBuild $GameDir
} elseif (Test-Sts2Running) {
    Write-Warning 'The game is already running; starting another instance.'
}

$arguments = @()
if (-not $Steam) { $arguments += @('--force-steam', 'off', '--clientId', $Profile) }
if (-not $NoWindowPosition) { $arguments += @('-wpos', 0, 0) }
if ($Gallery) { $arguments += '--spirekit-gallery' }
if ($Tavern) { $arguments += '--spirits-tavern' }
if ($OpenSettings) { $arguments += @('--spirekit-open-settings', $OpenSettings) }
if ($Scroll) { $arguments += @('--spirekit-scroll', $Scroll) }
if ($DumpScenes) { $arguments += @('--spirekit-dump-scenes', ($DumpScenes -join ',')) }

$what = if ($Steam) { 'Steam profile' } else { "test profile $Profile" }
Write-Host "Starting Slay the Spire 2 ($what)$(if ($Gallery) { ' with the controls gallery' })..."
Write-Host "Game: $GameDir"
Start-Sts2 $GameDir $arguments

Write-Host "Log: $(Get-Sts2LogPath)"
