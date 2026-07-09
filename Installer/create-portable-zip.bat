@echo off
REM ============================================================================
REM AT Studio - Portable ZIP-csomag keszitese
REM ============================================================================
REM Ez a szkript feltetelezi, hogy mar lefutott a self-contained publish
REM (lasd README_Installer.md 2. lepes), es a kimenet itt van:
REM   AT.App\bin\Release\net9.0-windows\win-x64\publish\
REM
REM Hasznalat: futtasd ezt a fajlt a solution gyokerebol (ahol az AT.sln van),
REM vagy ebbol a mappabol, ha a PUBLISH_DIR utvonalat hozzaigazitod.
REM ============================================================================

setlocal

set VERSION=1.0.0
set PUBLISH_DIR=AT.App\bin\Release\net9.0-windows\win-x64\publish
set OUTPUT_ZIP=AT-Studio-Portable-%VERSION%.zip

if not exist "%PUBLISH_DIR%" (
    echo HIBA: A publish mappa nem talalhato: %PUBLISH_DIR%
    echo Eloszor futtasd le a self-contained publish parancsot - lasd README_Installer.md 2. lepes.
    exit /b 1
)

echo Portable ZIP keszitese: %OUTPUT_ZIP%
echo Forras mappa: %PUBLISH_DIR%

REM A PowerShell Compress-Archive-t hasznaljuk, ez minden Windows 10/11-en
REM alapbol elerheto, nincs kulon eszkoz-fuggoseg.
powershell -NoProfile -Command "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%OUTPUT_ZIP%' -Force"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Kesz: %OUTPUT_ZIP%
    echo Ez a ZIP kicsomagolas utan azonnal futtathato - AT.App.exe -, nincs kulon telepites.
) else (
    echo HIBA: a ZIP keszitese sikertelen volt.
    exit /b 1
)

endlocal
