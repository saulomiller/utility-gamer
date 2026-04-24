param(
    [ValidateSet("win-x64")]
    [string]$RuntimeIdentifier = "win-x64",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Version = "1.0.0",

    [switch]$SkipPublish,

    [string]$InnoCompilerPath = ""
)

$ErrorActionPreference = "Stop"

function Find-InnoCompiler {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $resolved = Resolve-Path -LiteralPath $ExplicitPath -ErrorAction Stop
        return $resolved.Path
    }

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return ""
}

function Get-ProjectTargetFramework {
    param([string]$ProjectPath)

    [xml]$project = Get-Content -LiteralPath $ProjectPath
    foreach ($propertyGroup in $project.Project.PropertyGroup) {
        if (-not [string]::IsNullOrWhiteSpace($propertyGroup.TargetFramework)) {
            return $propertyGroup.TargetFramework
        }
    }

    throw "TargetFramework was not found in $ProjectPath."
}

function Repair-PublishedResourceIndex {
    param(
        [string]$RepoRoot,
        [string]$ProjectPath,
        [string]$Configuration,
        [string]$Platform,
        [string]$RuntimeIdentifier,
        [string]$SourceDir
    )

    $targetFramework = Get-ProjectTargetFramework $ProjectPath
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
    $buildPriCandidates = @(
        (Join-Path $RepoRoot "$projectName\bin\$Platform\$Configuration\$targetFramework\$RuntimeIdentifier\$projectName.pri"),
        (Join-Path $RepoRoot "$projectName\bin\$Configuration\$targetFramework\$RuntimeIdentifier\$projectName.pri")
    )
    $buildPri = $buildPriCandidates |
        Where-Object { Test-Path -LiteralPath $_ } |
        ForEach-Object { Get-Item -LiteralPath $_ } |
        Sort-Object Length -Descending |
        Select-Object -First 1
    $publishedPri = Join-Path $SourceDir "$projectName.pri"

    if ($null -eq $buildPri) {
        throw "Build resource index was not found. Checked: $($buildPriCandidates -join ', ')"
    }

    if (-not (Test-Path -LiteralPath $publishedPri)) {
        throw "Published resource index was not found: $publishedPri"
    }

    $publishedPriItem = Get-Item -LiteralPath $publishedPri

    if ($buildPri.Length -gt $publishedPriItem.Length) {
        Write-Host "Replacing published resource index with complete build resource index..."
        Write-Host "Resource index: $publishedPri"
        Copy-Item -LiteralPath $buildPri.FullName -Destination $publishedPri -Force
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "UtilityGamer.WinUI\UtilityGamer.WinUI.csproj"
$sourceDir = Join-Path $repoRoot "artifacts\bin-build\winui"
$outputDir = Join-Path $repoRoot "artifacts\installer"
$issPath = Join-Path $repoRoot "installer\UtilityGamer.iss"
$platform = switch ($RuntimeIdentifier) {
    "win-x64" { "x64" }
}

if (-not $SkipPublish) {
    Write-Host "Publishing Utility Gamer for installer ($RuntimeIdentifier, $Configuration)..."
    Write-Host "Output: $sourceDir"

    dotnet restore $projectPath `
        -r $RuntimeIdentifier `
        --ignore-failed-sources `
        -p:Platform=$platform `
        -p:NuGetAudit=false `
        -p:PublishReadyToRun=false `
        -p:WindowsAppSDKSelfContained=true

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed for $RuntimeIdentifier."
    }

    dotnet publish $projectPath `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --no-restore `
        --self-contained true `
        -p:Platform=$platform `
        -p:WindowsAppSDKSelfContained=true `
        -p:PublishTrimmed=false `
        -p:PublishSingleFile=false `
        -p:PublishReadyToRun=false `
        -p:NuGetAudit=false `
        -o $sourceDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $RuntimeIdentifier."
    }

    Repair-PublishedResourceIndex `
        -RepoRoot $repoRoot `
        -ProjectPath $projectPath `
        -Configuration $Configuration `
        -Platform $platform `
        -RuntimeIdentifier $RuntimeIdentifier `
        -SourceDir $sourceDir
}

$appExe = Join-Path $sourceDir "UtilityGamer.WinUI.exe"
if (-not (Test-Path -LiteralPath $appExe)) {
    throw "Installer source is missing $appExe. Run without -SkipPublish or build artifacts\bin-build\winui first."
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$compiler = Find-InnoCompiler $InnoCompilerPath
if ([string]::IsNullOrWhiteSpace($compiler)) {
    throw "Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup 6 or pass -InnoCompilerPath."
}

Write-Host "Building installer with Inno Setup..."
Write-Host "Compiler: $compiler"
Write-Host "Script: $issPath"
Write-Host "Output: $outputDir"

& $compiler `
    "/DAppVersion=$Version" `
    "/DSourceDir=$sourceDir" `
    "/DOutputDir=$outputDir" `
    $issPath

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compiler failed."
}

$installer = Join-Path $outputDir "UtilityGamerSetup-$Version-x64.exe"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer build completed, but expected output was not found: $installer"
}

Write-Host ""
Write-Host "Installer created:"
Write-Host $installer
