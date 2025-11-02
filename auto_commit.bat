@echo off
setlocal enabledelayedexpansion

:: Get the full path of this script
set "SCRIPT_PATH=%~f0"

:: Check if running with admin privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script requires administrative privileges.
    echo Please run as administrator.
    pause
    exit /b 1
)

:: Function to setup the scheduled task
:setup_task
schtasks /query /tn "KeePass Auto Commit" >nul 2>&1
if %errorlevel% equ 0 (
    echo Task already exists.
) else (
    echo Creating scheduled task...
    schtasks /create /tn "KeePass Auto Commit" /tr "\"%SCRIPT_PATH%\" run" /sc minute /mo 30 /ru SYSTEM /f
    if !errorlevel! equ 0 (
        echo Scheduled task created successfully.
    ) else (
        echo Failed to create scheduled task.
        exit /b 1
    )
)

:: Check if this is a scheduled run
if "%1"=="run" goto perform_git_ops

:: If no arguments, setup task and perform initial git operations
call :setup_task
goto perform_git_ops

:perform_git_ops
cd /d "%~dp0"
git checkout master
git pull origin master

:: Check for conflicts
git pull origin master >nul 2>&1
if errorlevel 1 (
    echo Git conflict detected. No actions taken. > auto_commit.log
    exit /b 1
)

git add .
git commit -m "Auto commit"
git push origin master
echo Auto commit done at %date% %time% > auto_commit.log
exit /b 0