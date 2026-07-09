@echo off
REM Script to launch Revit 2024

set "revitPath=C:\Program Files\Autodesk\Revit 2024\Revit.exe"

if exist "%revitPath%" (
    echo Launching Revit 2024...
    start "" "%revitPath%"
) else (
    echo Error: Revit 2024 not found at %revitPath%
    echo Please check if Revit 2024 is installed on this system.
    pause
)