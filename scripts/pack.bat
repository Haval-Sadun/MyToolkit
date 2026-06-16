@echo off
:: pack.bat — Pack the current version of MyToolkit to the local NuGet feed.
:: Does NOT bump the version. Use release.bat to bump + pack.
::
:: Usage: pack.bat

setlocal

set MYTOOLKIT_CSPROJ=%~dp0..\MyToolkit\MyToolkit.csproj
set LOCAL_FEED=C:\NuGet\test-packages

:: Read current version from the csproj
for /f "tokens=*" %%v in ('powershell -NoProfile -Command ^
  "([xml](Get-Content '%MYTOOLKIT_CSPROJ%')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1"') do (
  set CURRENT_VERSION=%%v
)

echo.
echo Packing Haval.MyToolkit v%CURRENT_VERSION% ...
echo Output: %LOCAL_FEED%
echo.

dotnet pack "%MYTOOLKIT_CSPROJ%" -c Release -o "%LOCAL_FEED%"
if errorlevel 1 (
  echo.
  echo ERROR: Pack failed.
  exit /b 1
)

echo.
echo Done.  Haval.MyToolkit.%CURRENT_VERSION%.nupkg is in %LOCAL_FEED%
endlocal
