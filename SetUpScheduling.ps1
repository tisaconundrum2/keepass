$taskName = "KeePass Auto Commit"
$commitScript = Join-Path $PSScriptRoot "auto_commit.bat"

if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Write-Host "Task already exists."
    exit 0
}

$action   = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c `"$commitScript`""
$trigger  = New-ScheduledTaskTrigger -RepetitionInterval (New-TimeSpan -Minutes 30) -Once -At (Get-Date)
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 5) -StartWhenAvailable

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -RunLevel Limited -Force

Write-Host "Scheduled task created successfully."
