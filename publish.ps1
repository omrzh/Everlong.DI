param(
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $repoRoot "publish"

# Clean publish directory
if (Test-Path $publishDir) {
  Remove-Item -Path "$publishDir\*" -Recurse -Force
} else {
  New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
}

Write-Host "=== Building & packing Everlong.DI ($Configuration) ===" -ForegroundColor Cyan

# Build all projects first (ensures analyzer DLLs exist in the right output paths)
dotnet build "$repoRoot\Everlong.DI.slnx" `
  --configuration $Configuration

# Pack the runtime project (embeds analyzers)
dotnet pack "$repoRoot\src\Everlong.DI\Everlong.DI.csproj" `
  --configuration $Configuration `
  --no-build `
  --output $publishDir

Write-Host ""
Write-Host "=== Published packages ===" -ForegroundColor Green
Get-ChildItem $publishDir -Filter "*.nupkg" | ForEach-Object {
  Write-Host "  $($_.Name)" -ForegroundColor White
}
Write-Host ""
Write-Host "Done → $publishDir" -ForegroundColor Green
