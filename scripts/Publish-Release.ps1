param(
    [ValidateSet("win-x86", "win-x64", "win-arm64")]
    [string]$RuntimeIdentifier = "win-x64",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "UtilityGamer.WinUI\UtilityGamer.WinUI.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$RuntimeIdentifier"

Write-Host "Publishing Utility Gamer for $RuntimeIdentifier ($Configuration)..."
Write-Host "Output: $publishDir"

dotnet restore $projectPath `
    -r $RuntimeIdentifier `
    --ignore-failed-sources `
    -p:NuGetAudit=false `
    -p:WindowsAppSDKSelfContained=true

if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed for $RuntimeIdentifier."
}

dotnet publish $projectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --no-restore `
    --self-contained true `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishTrimmed=false `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=$([string]($Configuration -eq "Release")) `
    -p:NuGetAudit=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for $RuntimeIdentifier."
}

Write-Host ""
Write-Host "Publish completed."
Write-Host "Deliver the entire folder below to the end user:"
Write-Host $publishDir
