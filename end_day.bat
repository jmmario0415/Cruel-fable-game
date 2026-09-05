@echo off
chcp 65001 >nul
title Fairy Tale - End Day
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\end-day.ps1"
echo.
pause
