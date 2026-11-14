<#
.SYNOPSIS
    Launches one Slay the Spire 2 instance with the mod, optionally rebuilding it first.

.DESCRIPTION
    By default the game starts without Steam on a local test profile (user://default/<Profile>/), so your real
    save is untouched; -Steam uses your normal Steam profile instead.

    The build skips deploying while the game is running, so -Build closes running instances first.

.EXAMPLE
    ./tools/launch.ps1 -Build -Gallery    # rebuild, then open the SpireKit controls gallery (F8 toggles it)
    ./tools/launch.ps1 -Steam             # your real profile
    ./tools/launch.ps1 -Profile 2         # another test profile
    ./tools/launch.ps1 -OpenSettings Spirits   # straight to a mod's Settings tab
    ./tools/launch.ps1 -DumpScenes res://scenes/screens/settings_screen.tscn   # node tree -> <mod storage>/dumps/
#>
[CmdletBinding()]
param(
    [string] $GameDir = 'E:\Games\Steam\steamapps\common\Slay the Spire 2',
    [switch] $Build,
    [switch] $Gallery,
    [switch] $Steam,
    [int] $Profile = 1,
    [string[]] $DumpScenes,
    [string] $OpenSettings,
    [int] $Scroll,
    [switch] $NoWindowPosition
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $GameDir 'SlayTheSpire2.exe'
if (-not (Test-Path $exe)) { throw "Game not found: $exe" }

$running = Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue
if ($running) {
    if ($Build) {
        Write-Host 'Closing the running game so the build can deploy...'
        $running | Stop-Process -Force
        $running | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue
    } else {
        Write-Warning 'The game is already running; starting another instance.'
    }
}

if ($Build) {
    dotnet build (Join-Path $PSScriptRoot '..\mod\src\SlayTheSpirits') -nologo -v m
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

$arguments = @()
if (-not $Steam) { $arguments += @('--force-steam', 'off', '--clientId', $Profile) }
if (-not $NoWindowPosition) { $arguments += @('-wpos', 0, 0) }
if ($Gallery) { $arguments += '--spirekit-gallery' }
if ($OpenSettings) { $arguments += @('--spirekit-open-settings', $OpenSettings) }
if ($Scroll) { $arguments += @('--spirekit-scroll', $Scroll) }
if ($DumpScenes) { $arguments += @('--spirekit-dump-scenes', ($DumpScenes -join ',')) }

$what = if ($Steam) { 'Steam profile' } else { "test profile $Profile" }
Write-Host "Starting Slay the Spire 2 ($what)$(if ($Gallery) { ' with the controls gallery' })..."
if ($arguments.Count -gt 0) {
    Start-Process -FilePath $exe -WorkingDirectory $GameDir -ArgumentList $arguments
} else {
    Start-Process -FilePath $exe -WorkingDirectory $GameDir
}

Write-Host "Log: $(Join-Path $env:APPDATA 'SlayTheSpire2\logs\godot.log')"
