@echo off
cd /d "%~dp0"
git add .
git commit -m "Auto commit" || echo No changes to commit.
git push --force origin master
echo Auto commit done at %date% %time% > auto_commit.log
exit /b 0