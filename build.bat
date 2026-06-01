@echo off
setlocal
cd /d "%~dp0"
dotnet build PerkShop.csproj -c Release
if errorlevel 1 exit /b %errorlevel%
if not exist dist mkdir dist
copy /Y "bin\Release\net6.0\PerkShop.dll" "dist\PerkShop.dll"
echo Done. DLL copied to dist\PerkShop.dll
