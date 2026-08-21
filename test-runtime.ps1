param(
    [string]$GameDir = $(if ($env:NUCLEAR_OPTION_DIR) { $env:NUCLEAR_OPTION_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option" })
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$csc = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$bepInEx = Join-Path $GameDir "BepInEx\core"
$pluginDir = Join-Path $root "bin\Release"
$testDir = Join-Path $root "tests\bin"
$testExe = Join-Path $testDir "HarmonySmokeTest.exe"
$ripperDll = Join-Path (Split-Path $root -Parent) "NuclearOptionStatsBridge\bin\Release\KellysRipperControlBridge.dll"

& (Join-Path $root "build-runtime.ps1") -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force -Path $testDir | Out-Null
$references = @(
    (Join-Path $bepInEx "BepInEx.dll"),
    (Join-Path $bepInEx "0Harmony.dll")
)
$arguments = @("/nologo", "/target:exe", "/langversion:latest", "/out:$testExe")
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += (Join-Path $root "tests\HarmonySmokeTest.cs")
& $csc @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item -LiteralPath (Join-Path $bepInEx "BepInEx.dll") -Destination $testDir -Force
Copy-Item -LiteralPath (Join-Path $bepInEx "0Harmony.dll") -Destination $testDir -Force
if (!(Test-Path -LiteralPath $ripperDll)) { throw "Current RIPPER bridge build was not found: $ripperDll" }
& $testExe $GameDir $pluginDir $ripperDll
exit $LASTEXITCODE
