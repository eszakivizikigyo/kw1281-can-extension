# Publish CLI and UI for all platforms
param(
    [string]$OutputDir = ".\GitHub"
)

$ErrorActionPreference = 'Stop'

$Rids = @('win-x64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')

# Read version from library project
$ProjectXml = [xml](Get-Content .\kw1281test.csproj)
$Version = $ProjectXml.Project.PropertyGroup.Version
Write-Host "Publishing version $Version" -ForegroundColor Cyan

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

foreach ($rid in $Rids) {
    Write-Host "`nPublishing CLI for $rid..." -ForegroundColor Yellow
    dotnet publish Cli/kw1281test.Cli.csproj `
        -c Release -r $rid --self-contained `
        -p:PublishSingleFile=true `
        -o "$OutputDir/cli-$rid"

    Write-Host "Publishing UI for $rid..." -ForegroundColor Yellow
    dotnet publish Ui/kw1281test.Ui.csproj `
        -c Release -r $rid --self-contained `
        -p:PublishSingleFile=true `
        -o "$OutputDir/ui-$rid"
}

# Create zip archives
Write-Host "`nCreating archives..." -ForegroundColor Cyan

foreach ($rid in $Rids) {
    $CliDir = "$OutputDir/cli-$rid"
    $UiDir  = "$OutputDir/ui-$rid"

    if ($rid -like 'win*') {
        $CliExe = "$CliDir/kw1281test.cli.exe"
        $UiExe  = "$UiDir/kw1281test.ui.exe"
    } else {
        $CliExe = "$CliDir/kw1281test.cli"
        $UiExe  = "$UiDir/kw1281test.ui"
    }

    if (Test-Path $CliExe) {
        Compress-Archive -Force -Path $CliExe -DestinationPath "$OutputDir/kw1281test-cli_${Version}_$rid.zip"
    }
    if (Test-Path $UiExe) {
        Compress-Archive -Force -Path $UiExe -DestinationPath "$OutputDir/kw1281test-ui_${Version}_$rid.zip"
    }
}

Write-Host "`nDone! Archives in $OutputDir" -ForegroundColor Green
Get-ChildItem "$OutputDir/*.zip" | Format-Table Name, Length
