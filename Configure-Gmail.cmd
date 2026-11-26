@echo off
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0Configure-Gmail.ps1"
set "UnitasGmailExit=%ERRORLEVEL%"
pause
exit /b %UnitasGmailExit%
