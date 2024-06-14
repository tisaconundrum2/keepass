@echo off
REM Create a scheduled task to run the auto_commit.sh script every 30 minutes upon logging in

REM Define the task name
set TaskName=AutoCommitKeepass

REM Define the script path
set ScriptPath=C:\repos\keepass\auto_commit.sh

REM Create the scheduled task to run every 30 minutes
schtasks /create /tn %TaskName% /tr "%ScriptPath%" /sc minute /mo 30 /ru %username% /it /f

REM Create a scheduled task to run at logon
schtasks /create /tn %TaskName%_OnLogon /tr "%ScriptPath%" /sc onlogon /ru %username% /it /f

echo Scheduled tasks "%TaskName%" and "%TaskName%_OnLogon" created to run "%ScriptPath%" every 30 minutes and upon logging in.

pause
