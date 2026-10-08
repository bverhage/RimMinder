<#
.SYNOPSIS
  Starts RimWorld in an isolated test setup: only Core, DLCs, Harmony and RimMinder.

.DESCRIPTION
  Uses RimWorld's -savedatafolder option so your normal mod list, settings and saves
  are not touched. Test config, saves, log and screenshots go to .testdata\ (git-ignored).

  -SelfTest  also loads the RimMinder.SelfTest dev mod, starts a quick-test colony,
             runs the automated tests and writes .testdata\SelfTest\results.txt plus screenshots.
  -Quit      closes the game when the self-test is done (for unattended runs).

.EXAMPLE
  .\tools\test-launch.ps1                  # clean test colony to play around in
  .\tools\test-launch.ps1 -SelfTest -Quit  # automated test run
  .\tools\test-launch.ps1 -Screenshots "Hatleia Confederation"  # Steam screenshots of a save
#>
param(
    [switch]$SelfTest,
    [switch]$Quit,
    [switch]$NoBuild,
    # Name of a save in .testdata\Saves: load it and take Steam screenshots (fullscreen 1920x1080).
    [string]$Screenshots
)

$ErrorActionPreference = 'Stop'
$useTestMod = $SelfTest -or $Screenshots
$root     = Split-Path -Parent $PSScriptRoot
$game     = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld'
$mods     = Join-Path $game 'Mods'
$testData = Join-Path $root '.testdata'
$realCfg  = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config'

if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) {
    throw 'RimWorld is already running. Close it first.'
}

# 1. Build
if (-not $NoBuild) {
    $project = if ($useTestMod) { 'Source\RimMinder.SelfTest' } else { 'Source\RimMinder' }
    & dotnet build (Join-Path $root $project) -c Release -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

# 2. Make sure the game can see the mods (junctions point back into this repo)
function Ensure-Link($name, $target) {
    $link = Join-Path $mods $name
    if (-not (Test-Path $link)) {
        New-Item -ItemType Junction -Path $link -Target $target | Out-Null
        Write-Host "Linked $link -> $target"
    }
}
Ensure-Link 'RimMinder' (Join-Path $root 'RimMinder')
if ($useTestMod) { Ensure-Link 'RimMinder.SelfTest' (Join-Path $root 'Tests\RimMinder.SelfTest') }

# 3. Isolated config: minimal mod list, windowed, dev mode on
$cfg = Join-Path $testData 'Config'
New-Item -ItemType Directory -Force $cfg | Out-Null

# DLCs must load in release order (e.g. Odyssey builds on Royalty defs), so don't sort by folder name.
$releaseOrder = 'Royalty', 'Ideology', 'Biotech', 'Anomaly', 'Odyssey'
$installed = (Get-ChildItem (Join-Path $game 'Data') -Directory).Name
$expansions = $releaseOrder | Where-Object { $installed -contains $_ } |
    ForEach-Object { 'ludeon.rimworld.' + $_.ToLower() }
$active = @('brrainz.harmony', 'ludeon.rimworld') + $expansions + @('theB52.RimMinder')
if ($useTestMod) { $active += 'theB52.RimMinder.SelfTest' }
$version = (Get-Content (Join-Path $game 'Version.txt') -Raw).Trim()

$xml = @("<?xml version=""1.0"" encoding=""utf-8""?>", '<ModsConfigData>', "  <version>$version</version>", '  <activeMods>')
$xml += $active | ForEach-Object { "    <li>$_</li>" }
$xml += '  </activeMods>', '  <knownExpansions>'
$xml += $expansions | ForEach-Object { "    <li>$_</li>" }
$xml += '  </knownExpansions>', '</ModsConfigData>'
Set-Content -Encoding UTF8 (Join-Path $cfg 'ModsConfig.xml') $xml

$prefsPath = Join-Path $cfg 'Prefs.xml'
if (Test-Path (Join-Path $realCfg 'Prefs.xml')) {
    [xml]$prefs = Get-Content (Join-Path $realCfg 'Prefs.xml')
    $p = $prefs.PrefsData
    $p.fullscreen = if ($Screenshots) { 'True' } else { 'False' }
    $p.screenWidth = if ($Screenshots) { '1920' } else { '1600' }
    $p.screenHeight = if ($Screenshots) { '1080' } else { '900' }
    $p.runInBackground = 'True'
    # Screenshots: no dev toolbar, no auto-opening debug log, no learning helper.
    $p.devMode = if ($Screenshots) { 'False' } else { 'True' }
    if ($Screenshots) { $p.adaptiveTrainingEnabled = 'False' }
    $prefs.Save($prefsPath)
}

# 4. Launch
$log = Join-Path $testData 'Player.log'
$gameArgs = @("-savedatafolder=$testData", '-logFile', $log)
if (-not $Screenshots) { $gameArgs += '-quicktest' }
if ($Screenshots) { $gameArgs += "`"-rimminder-screenshots=$Screenshots`"", '-rimminder-quit' }
if ($SelfTest) { $gameArgs += '-rimminder-selftest' }
if ($Quit) { $gameArgs += '-rimminder-quit' }

Write-Host "Starting RimWorld (test data: $testData)"
$proc = Start-Process (Join-Path $game 'RimWorldWin64.exe') -ArgumentList $gameArgs -PassThru

if (($SelfTest -and $Quit) -or $Screenshots) {
    $proc.WaitForExit()
    $results = Join-Path $testData $(if ($Screenshots) { 'Screenshots\results.txt' } else { 'SelfTest\results.txt' })
    if (Test-Path $results) {
        Get-Content $results
    } else {
        Write-Warning "No results file. Check the log: $log"
    }
}
