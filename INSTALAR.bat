@echo off
title Instalador Glaux Tools
echo ==========================================================
echo           INSTALANDO GLAUX TOOLS NO GRASSHOPPER...
echo ==========================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
echo.
pause
