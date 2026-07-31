@echo off
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "COMMIT_SCRIPT=%SCRIPT_DIR%auto_commit.bat"

schtasks /query /tn "KeePass Auto Commit" >nul 2>&1
if %errorlevel% equ 0 (
    echo Task already exists.
) else (
    echo Creating scheduled task...
    schtasks /create /tn "KeePass Auto Commit" /tr "cmd /c \"%COMMIT_SCRIPT%\"" /sc minute /mo 30 /f
    if !errorlevel! equ 0 (
        echo Scheduled task created successfully.
    ) else (
        echo Failed to create scheduled task.
        exit /b 1
    )
)
