@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

call codex.cmd ^
  -C "%SCRIPT_DIR%" ^
  --dangerously-bypass-approvals-and-sandbox ^
  %*

exit /b %errorlevel%