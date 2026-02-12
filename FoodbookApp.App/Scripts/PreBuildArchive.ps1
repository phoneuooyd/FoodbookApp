# Pre-build script to create safety archive BEFORE deployment
# This ensures data survives Visual Studio's APK reinstall that wipes /data/data/

param(
    [string]$Configuration,
    [string]$TargetFramework
)

Write-Host "PreBuildArchive: Configuration=$Configuration, TargetFramework=$TargetFramework"

# Only run for Android Debug builds (where Fast Deployment causes issues)
if ($TargetFramework -notlike "net*-android*") {
    Write-Host "PreBuildArchive: Skipping (not Android)"
    exit 0
}

# Create deployment marker in external storage location
# This will be detected by the app on next startup to trigger auto-restore
$markerPath = "$env:USERPROFILE\.foodbook_deployment_marker"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
Set-Content -Path $markerPath -Value "Deployment at $timestamp`nConfiguration: $Configuration"

Write-Host "PreBuildArchive: Created deployment marker at $markerPath"
Write-Host "App will detect this marker and attempt restore from safety archive on next launch"
