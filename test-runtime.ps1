param(
    [string]$GameDir = $(if ($env:NUCLEAR_OPTION_DIR) { $env:NUCLEAR_OPTION_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option" }),
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$csc = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$bepInEx = Join-Path $GameDir "BepInEx\core"
$pluginDir = Join-Path $root "bin\$Configuration"
$testDir = Join-Path $root "tests\bin"
$testExe = Join-Path $testDir "HarmonySmokeTest.exe"
$ripperDll = Join-Path (Split-Path $root -Parent) "NuclearOptionStatsBridge\bin\Release\KellysRipperControlBridge.dll"

& (Join-Path $root "build-runtime.ps1") -GameDir $GameDir -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force -Path $testDir | Out-Null
$policyTest = Join-Path $testDir "CombatPolicyTests.exe"
& $csc /nologo /target:exe /langversion:latest "/out:$policyTest" (Join-Path $root "CombatPolicy.cs") (Join-Path $root "tests\CombatPolicyTests.cs")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $policyTest
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
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
