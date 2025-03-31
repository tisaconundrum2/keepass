# Publish the .NET application
Set-Location $PSScriptRoot

# Ensure the script is running as Administrator
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "This script must be run as Administrator. Relaunching with elevated privileges..."
    Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

# Add the repository as a safe directory for Git
Write-Host "Configuring Git to mark the repository as safe..."

dotnet publish -c Release -r win-x64 --self-contained true --output $PSScriptRoot\bin\Release\KeePass.Background.Service\win-x64\publish

# Delete the existing service if it exists
if (Get-Service -Name "KeePassBackgroundService" -ErrorAction SilentlyContinue) {
    Write-Host "Stopping and removing existing KeePass Background Service..."
    Stop-Service -Name "KeePassBackgroundService" -Force
    sc.exe delete KeePassBackgroundService
}
else {
    Write-Host "No existing KeePass Background Service found."
}

# Create the Windows service
New-Service -Name "KeePassBackgroundService" `
            -BinaryPathName "$PSScriptRoot\bin\Release\KeePass.Background.Service\win-x64\publish\KeePass.Background.Service.exe" `
            -StartupType Automatic `
            -Description "KeePass Background Service"

# Start the Windows service
Start-Service -Name "KeePassBackgroundService"

Write-Host "KeePass Background Service installed and started."
Pause