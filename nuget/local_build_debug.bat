@echo off

for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Sec=%dt:~12,2%"

set "timestamp=%HH%%Min%%Sec%"

set "build=build%timestamp%"
echo build: "%build%"

dotnet restore ..\src\Spreads.Core\Spreads.Core.csproj
dotnet pack ..\src\Spreads.Core\Spreads.Core.csproj -c Debug -o \transient\LocalNuget  --version-suffix "%build%" -p:AutoSuffix=True

REM dotnet restore ..\src\Spreads\Spreads.csproj
REM dotnet pack ..\src\Spreads\Spreads.csproj -c Debug -o \transient\LocalNuget --version-suffix "%build%" -p:AutoSuffix=True

pause