@echo off
chcp 65001 >nul
title Fairy Tale - Setup
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\setup.ps1"
echo.
pause
