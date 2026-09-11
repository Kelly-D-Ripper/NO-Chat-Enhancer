param(
    [string]$GameDir = $(if ($env:NUCLEAR_OPTION_DIR) { $env:NUCLEAR_OPTION_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option" }),
    [string]$BepInExCoreDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "NuclearOptionStatsBridge\vendor\BepInEx-5.4.22\extracted\BepInEx\core"),
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$csc = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$managed = Join-Path $GameDir "NuclearOption_Data\Managed"
$bepInEx = $BepInExCoreDir
$outDir = Join-Path $root "bin\$Configuration"
$outDll = Join-Path $outDir "NuclearOptionChatEnhancer.dll"

if (!(Test-Path -LiteralPath $csc)) { throw "Roslyn csc.exe was not found at $csc" }

$references = @(
    (Join-Path $managed "mscorlib.dll"),
    (Join-Path $managed "System.dll"),
    (Join-Path $managed "System.Core.dll"),
    (Join-Path $managed "netstandard.dll"),
    (Join-Path $bepInEx "BepInEx.dll"),
    (Join-Path $bepInEx "0Harmony.dll"),
    (Join-Path $managed "Assembly-CSharp.dll"),
    (Join-Path $managed "Mirage.dll"),
    (Join-Path $managed "Unity.TextMeshPro.dll"),
    (Join-Path $managed "UnityEngine.dll"),
    (Join-Path $managed "UnityEngine.CoreModule.dll"),
    (Join-Path $managed "UnityEngine.IMGUIModule.dll"),
    (Join-Path $managed "UnityEngine.InputLegacyModule.dll"),
    (Join-Path $managed "UnityEngine.TextRenderingModule.dll"),
    (Join-Path $managed "UnityEngine.UI.dll"),
    (Join-Path $managed "UnityEngine.UIModule.dll")
)

foreach ($path in $references) {
    if (!(Test-Path -LiteralPath $path)) { throw "Required reference was not found: $path" }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$arguments = @("/nologo", "/nostdlib+", "/target:library", "/langversion:latest", "/optimize+", "/deterministic+", "/out:$outDll")
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += (Join-Path $root "Plugin.cs")
$arguments += (Join-Path $root "CombatFeed.cs")
$arguments += (Join-Path $root "CombatPolicy.cs")

& $csc @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built $outDll"
