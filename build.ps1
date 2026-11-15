param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    
    [switch]$NoRestore,
    [switch]$Help,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

# Read version from project file
$projectFile = "src\OllamaAgent\OllamaAgent.csproj"
$version = "1.0.0"
if (Test-Path $projectFile) {
    $content = Get-Content $projectFile -Raw
    if ($content -match 'Version>(\d+\.\d+\.\d+)') {
        $version = $Matches[1]
    }
}

$ErrorActionPreference = "Stop"

# Colors for output
$ColorGreen = "#4EC9B0"
$ColorYellow = "#DCDCAA"
$ColorRed = "#F44747"
$ColorCyan = "#569CD6"

function Write-Log($message, $color = $ColorCyan) {
    Write-Host "  $message" -ForegroundColor $color
}

function Write-Success($message) {
    Write-Log $message $ColorGreen
}

function Write-Warn($message) {
    Write-Host "  $message" -ForegroundColor $ColorYellow
}

function Write-Error-Exit($message) {
    Write-Host "  ERROR: $message" -ForegroundColor $ColorRed
    exit 1
}

if ($Help) {
    Write-Host ""
    Write-Host "Build script for OllamaAgent v$version" -ForegroundColor $ColorCyan
    Write-Host ""
    Write-Host "Usage: .\build.ps1 [options]" -ForegroundColor $ColorYellow
    Write-Host ""
    Write-Host "Options:" -ForegroundColor $ColorCyan
    Write-Host "  -Configuration   Build configuration: Debug (default) or Release"
    Write-Host "  -NoRestore       Skip NuGet restore"
    Write-Host "  -SkipTests       Skip running tests"
    Write-Host "  -Help            Show this help message"
    Write-Host ""
    exit 0
}

Write-Host ""
Write-Log "========================================"
Write-Log "  OllamaAgent Build"
Write-Log "========================================" -ForegroundColor $ColorCyan
Write-Host ""

# Check .NET SDK
Write-Log "Checking .NET SDK..."
$dotnetVersion = dotnet --version
Write-Log ".NET SDK version: $dotnetVersion"

if ($LASTEXITCODE -ne 0) {
    Write-Error-Exit ".NET SDK not found. Please install .NET 8.0+."
}

# Restore packages
if (-not $NoRestore) {
    Write-Log "Restoring NuGet packages..."
    dotnet restore OllamaAgent.sln
    if ($LASTEXITCODE -ne 0) {
        Write-Error-Exit "NuGet restore failed."
    }
    Write-Success "Packages restored."
    Write-Host ""
}

# Build
Write-Log "Building in $Configuration mode..."
dotnet build OllamaAgent.sln --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error-Exit "Build failed."
}
Write-Success "Build succeeded."
Write-Host ""

# Run tests
if (-not $SkipTests) {
    Write-Log "Running tests..."
    dotnet test --configuration $Configuration --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Warn "Some tests failed. Build succeeded but tests did not pass."
    }
    Write-Host ""
} else {
    Write-Log "Skipping tests (-SkipTests flag)"
    Write-Host ""
}

# Publish for installer (Release mode)
if ($Configuration -eq "Release") {
    Write-Log "Publishing application for installer..."
    dotnet publish src/OllamaAgent/OllamaAgent.csproj -c Release -o publish
    
    if (Test-Path "installer\makensis.exe") {
        Write-Log "Creating installer with NSIS..."
        & "installer\makensis.exe" setup.nsi
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Installer created: OllamaAgent_Setup.exe"
        }
        else {
            Write-Warn "NSIS installer creation failed. Make sure makensis.exe is available."
        }
    }
    else {
        Write-Warn "NSIS not found. Install NSIS and place makensis.exe in installer/ folder."
        Write-Log "To create installer manually:"
        Write-Log "  1. Install NSIS from https://nsis.sourceforge.io/Download"
        Write-Log "  2. Run: makensis.exe installer\setup.nsi"
    }
    Write-Host ""
}

# Summary
Write-Log "========================================" -ForegroundColor $ColorCyan
Write-Success "Build completed successfully! (v$version)"
Write-Log "========================================" -ForegroundColor $ColorCyan
Write-Host ""
