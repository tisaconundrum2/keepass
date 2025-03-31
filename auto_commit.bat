@echo off
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