<#
.SYNOPSIS
    Shared helpers for the tools/ scripts. Dot-source it: . "$PSScriptRoot\common.ps1"

.DESCRIPTION
    Finds the Slay the Spire 2 install so no script needs a machine-specific path: the script's -GameDir if
    given, otherwise whatever the build uses (build/Sts2.GameDir.props: STS2_GAME_DIR, then local.props, then
    Steam). Asking MSBuild keeps the scripts and the build in agreement.

    Works in Windows PowerShell 5.1 and PowerShell 7.
#>

Set-StrictMode -Version Latest

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$script:Sts2ExeName = 'SlayTheSpire2.exe'
$script:ModProject = Join-Path $script:RepoRoot 'mod\src\SlayTheSpirits'

<#
.SYNOPSIS
    Evaluates the mod project and returns the named MSBuild properties as an object (no build is run).
#>
function Get-BuildProperties([Parameter(Mandatory)] [string[]] $Names, [string[]] $MSBuildArgs = @()) {
    # -getProperty prints JSON for two or more properties, so always ask for at least two.
    $query = @($Names) + 'MSBuildProjectName' | Select-Object -Unique | ForEach-Object { "-getProperty:$_" }
    $json = dotnet msbuild $script:ModProject -nologo @query @MSBuildArgs
    if ($LASTEXITCODE -ne 0) { throw "Couldn't evaluate the build:`n$($json -join "`n")" }
    return ($json -join "`n" | ConvertFrom-Json).Properties
}

function Get-BuildGameDir {
    return Get-BuildProperties Sts2GameDir, Sts2GameDirSource
}

<#
.SYNOPSIS
    Returns the Slay the Spire 2 install folder, or throws saying how to set it.
#>
function Resolve-Sts2GameDir([string] $GameDir) {
    if ($GameDir) {
        $source = '-GameDir'
    } else {
        $found = Get-BuildGameDir
        $GameDir = $found.Sts2GameDir
        $source = $found.Sts2GameDirSource
    }
    if (-not $GameDir) {
        throw ("Couldn't find Slay the Spire 2 in your Steam libraries. Pass -GameDir, set `$env:STS2_GAME_DIR, " +
            "or set Sts2GameDir in local.props (copy local.props.example).")
    }
    if (-not (Test-Path -LiteralPath (Join-Path $GameDir $script:Sts2ExeName) -PathType Leaf)) {
        throw "$script:Sts2ExeName not found in '$GameDir' (from $source)."
    }
    Write-Verbose "Game folder from ${source}: $GameDir"
    return (Resolve-Path -LiteralPath $GameDir).Path
}

<#
.SYNOPSIS
    Closes running game instances (the build can't deploy over a locked DLL) and builds/deploys the mod.
#>
function Invoke-ModBuild([Parameter(Mandatory)] [string] $GameDir) {
    Stop-Sts2
    # Pass the folder through in case it came from -GameDir; always deploy, since that's the point of -Build.
    dotnet build $script:ModProject -nologo -v m "-p:Sts2GameDir=$GameDir" '-p:DeployMods=true'
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

function Stop-Sts2 {
    $running = Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($script:Sts2ExeName)) -ErrorAction SilentlyContinue
    if (-not $running) { return }
    Write-Host 'Closing the running game so the build can deploy...'
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue
}

function Test-Sts2Running {
    return [bool](Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($script:Sts2ExeName)) -ErrorAction SilentlyContinue)
}

function Start-Sts2([Parameter(Mandatory)] [string] $GameDir, [object[]] $Arguments = @()) {
    $exe = Join-Path $GameDir $script:Sts2ExeName
    if ($Arguments.Count -gt 0) {
        Start-Process -FilePath $exe -WorkingDirectory $GameDir -ArgumentList $Arguments
    } else {
        Start-Process -FilePath $exe -WorkingDirectory $GameDir
    }
}

function Get-Sts2LogPath {
    return Join-Path $env:APPDATA 'SlayTheSpire2\logs\godot.log'
}

<#
.SYNOPSIS
    Top-left corners of a 2x2 grid over the primary screen's working area (left-to-right, top-to-bottom).
#>
function Get-WindowGrid {
    $area = $null
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        $area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    } catch {
        Write-Verbose "Couldn't read the screen size; assuming 1920x1080."
    }
    $width = if ($area) { $area.Width } else { 1920 }
    $height = if ($area) { $area.Height } else { 1080 }
    $halfW = [int]($width / 2)
    $halfH = [int]($height / 2)
    return @(@(0, 0), @($halfW, 0), @(0, $halfH), @($halfW, $halfH))
}
