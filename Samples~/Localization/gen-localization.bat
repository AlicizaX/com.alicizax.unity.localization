@echo off
setlocal DisableDelayedExpansion
set "CONFIG=%~dp0Localization.ini"
if not exist "%CONFIG%" (
    echo Localization config not found: "%CONFIG%" 1>&2
    exit /b 1
)

set "UNITY_PROJECT="
for /f "usebackq tokens=1,* delims==" %%A in ("%CONFIG%") do if /i "%%A"=="unity_project" set "UNITY_PROJECT=%%B"
if not defined UNITY_PROJECT (
    echo unity_project is missing in "%CONFIG%". 1>&2
    exit /b 1
)
for %%I in ("%~dp0%UNITY_PROJECT%") do set "PROJECT_ROOT=%%~fI"
if not exist "%PROJECT_ROOT%\Assets\" (
    echo Unity project not found: "%PROJECT_ROOT%" 1>&2
    exit /b 1
)

set "PACKAGE_ROOT=%PROJECT_ROOT%\Packages\com.alicizax.unity.localization"
if not exist "%PACKAGE_ROOT%\Editor\Generation\Cli~\Localization.Cli.dll" (
    set "PACKAGE_ROOT="
    for /d %%P in ("%PROJECT_ROOT%\Library\PackageCache\com.alicizax.unity.localization@*") do if exist "%%~fP\Editor\Generation\Cli~\Localization.Cli.dll" (
        if defined PACKAGE_ROOT (
            echo Multiple localization packages found in PackageCache. 1>&2
            exit /b 1
        )
        set "PACKAGE_ROOT=%%~fP"
    )
)
if not defined PACKAGE_ROOT (
    echo Localization CLI not found in "%PROJECT_ROOT%\Packages" or PackageCache. 1>&2
    exit /b 1
)
where dotnet >nul 2>&1
if errorlevel 1 (
    echo dotnet was not found on PATH. 1>&2
    exit /b 1
)

dotnet "%PACKAGE_ROOT%\Editor\Generation\Cli~\Localization.Cli.dll" generate --config "%CONFIG%" --project "%PROJECT_ROOT%" --package "%PACKAGE_ROOT%"
exit /b %errorlevel%
