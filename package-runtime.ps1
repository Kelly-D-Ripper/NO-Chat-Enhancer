param(
    [string]$GameDir = $(if ($env:NUCLEAR_OPTION_DIR) { $env:NUCLEAR_OPTION_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option" }),
    [string]$Configuration = "Release",
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
. (Join-Path (Split-Path $root -Parent) "build-tools\New-CrossPlatformZip.ps1")
$stage = Join-Path $root ("package\stage-chat-" + [Guid]::NewGuid().ToString("N"))
$plugins = Join-Path $stage "BepInEx\plugins"
$zip = Join-Path $root ("NuclearOptionChatEnhancer-Client-" + $Version + ".zip")
$resolvedPackage = [IO.Path]::GetFullPath((Join-Path $root "package")).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$resolvedStage = [IO.Path]::GetFullPath($stage)
if (!$resolvedStage.StartsWith($resolvedPackage, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe staging path: $resolvedStage" }

& (Join-Path $root "test-runtime.ps1") -GameDir $GameDir -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force -Path $plugins | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $root "bin\$Configuration\NuclearOptionChatEnhancer.dll") -Destination $plugins
    Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination $stage
    Copy-Item -LiteralPath (Join-Path $root ("RELEASE-NOTES-" + $Version + ".md")) -Destination (Join-Path $stage "RELEASE-NOTES.md")
    New-CrossPlatformZip -SourceDirectory $stage -DestinationPath $zip
    Write-Host "Packaged $zip"
}
finally {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
