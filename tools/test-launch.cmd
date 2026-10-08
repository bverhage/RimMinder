@echo off
rem Runs test-launch.ps1 without changing the system's PowerShell execution policy:
rem the bypass only applies to this one process.
rem   tools\test-launch.cmd                  clean test colony
rem   tools\test-launch.cmd -SelfTest -Quit  automated tests
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0test-launch.ps1" %*
