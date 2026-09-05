@echo off
chcp 65001 >nul
title Fairy Tale - Start Day
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\start-day.ps1"
echo.
pause
