@echo off
chcp 65001 >nul
title Fairy Tale - Sync
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\sync.ps1"
echo.
pause
