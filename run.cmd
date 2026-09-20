@echo off
rem Double-click launcher: runs run.ps1 with the execution policy bypassed for this one process (downloaded ZIPs are blocked by default).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" %*
pause
