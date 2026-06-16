@echo off
:: release.bat — Bump MyToolkit version, pack, push to local feed, and update
::               the PackageReference version in all consuming apps.
::
:: Usage:  release.bat <new-version>
:: Example: release.bat 1.1.0

setlocal enabledelayedexpansion

if "%~1"=="" (
  echo Usage: release.bat ^<new-version^>
  echo Example: release.bat 1.1.0
  exit /b 1
)

set NEW_VERSION=%~1

:: ── Paths ────────────────────────────────────────────────────────────────────
set SHARED_ROOT=%~dp0..
set MYTOOLKIT_CSPROJ=%SHARED_ROOT%\MyToolkit\MyToolkit.csproj

set KC_CSPROJ=%SHARED_ROOT%\..\Kurdish_Community\mobile\KurdishConnect\kurdish_maui.csproj
set SB_MOBILE_CSPROJ=%SHARED_ROOT%\..\SyriaBet\frontend\SyriaBet.Mobile\SyriaBet.Mobile.csproj
set SB_ADMIN_CSPROJ=%SHARED_ROOT%\..\SyriaBet\frontend\SyriaBet.Admin\SyriaBet.Admin.csproj

set LOCAL_FEED=C:\NuGet\test-packages

:: ── Validate version format (digits and dots only) ───────────────────────────
echo %NEW_VERSION%| findstr /r "^[0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*$" >nul
if errorlevel 1 (
  echo ERROR: Version must be in MAJOR.MINOR.PATCH format ^(e.g. 1.2.0^)
  exit /b 1
)

:: ── Read current version ─────────────────────────────────────────────────────
for /f "tokens=*" %%v in ('powershell -NoProfile -Command ^
  "([xml](Get-Content '%MYTOOLKIT_CSPROJ%')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1"') do (
  set OLD_VERSION=%%v
)

if "%OLD_VERSION%"=="%NEW_VERSION%" (
  echo WARNING: New version is the same as the current version ^(%OLD_VERSION%^).
  set /p CONTINUE=Continue anyway? [y/N]:
  if /i not "!CONTINUE!"=="y" exit /b 0
)

echo.
echo Releasing Haval.MyToolkit
echo   %OLD_VERSION%  --^>  %NEW_VERSION%
echo.

:: ── 1. Bump version in MyToolkit.csproj ──────────────────────────────────────
echo [1/5] Updating MyToolkit.csproj ...
powershell -NoProfile -Command ^
  "$f = '%MYTOOLKIT_CSPROJ%';" ^
  "$c = Get-Content $f -Raw;" ^
  "$c = $c -replace '<Version>%OLD_VERSION%</Version>', '<Version>%NEW_VERSION%</Version>';" ^
  "Set-Content $f $c -NoNewline"
if errorlevel 1 goto :error

:: ── 2. Update PackageReference in consuming apps ──────────────────────────────
echo [2/5] Updating KurdishConnect ...
powershell -NoProfile -Command ^
  "$f = '%KC_CSPROJ%';" ^
  "$c = Get-Content $f -Raw;" ^
  "$c = $c -replace 'Include=""Haval\.MyToolkit"" Version=""%OLD_VERSION%""', 'Include=""Haval.MyToolkit"" Version=""%NEW_VERSION%""';" ^
  "Set-Content $f $c -NoNewline"
if errorlevel 1 goto :error

echo [3/5] Updating SyriaBet.Mobile ...
powershell -NoProfile -Command ^
  "$f = '%SB_MOBILE_CSPROJ%';" ^
  "$c = Get-Content $f -Raw;" ^
  "$c = $c -replace 'Include=""Haval\.MyToolkit"" Version=""%OLD_VERSION%""', 'Include=""Haval.MyToolkit"" Version=""%NEW_VERSION%""';" ^
  "Set-Content $f $c -NoNewline"
if errorlevel 1 goto :error

echo [4/5] Updating SyriaBet.Admin ...
powershell -NoProfile -Command ^
  "$f = '%SB_ADMIN_CSPROJ%';" ^
  "$c = Get-Content $f -Raw;" ^
  "$c = $c -replace 'Include=""Haval\.MyToolkit"" Version=""%OLD_VERSION%""', 'Include=""Haval.MyToolkit"" Version=""%NEW_VERSION%""';" ^
  "Set-Content $f $c -NoNewline"
if errorlevel 1 goto :error

:: ── 3. Pack ───────────────────────────────────────────────────────────────────
echo [5/5] Packing ...
dotnet pack "%MYTOOLKIT_CSPROJ%" -c Release -o "%LOCAL_FEED%"
if errorlevel 1 goto :error

echo.
echo ── Release complete ──────────────────────────────────────────────────────
echo   Package : %LOCAL_FEED%\Haval.MyToolkit.%NEW_VERSION%.nupkg
echo   Apps    : update to this version is already written to all three csproj files.
echo   Next    : run 'dotnet restore' in each app to pick up the new package.
echo.
endlocal
exit /b 0

:error
echo.
echo ERROR: Step failed. Check output above.
endlocal
exit /b 1
