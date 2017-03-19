@echo off
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Sec=%dt:~12,2%"

REM set "datestamp=%YYYY%%MM%%DD%" & set "timestamp=%HH%%Min%%Sec%"
set "fullstamp=%YY%%MM%%DD%%HH%%Min%"
REM echo datestamp: "%datestamp%"
REM echo timestamp: "%timestamp%"
REM echo fullstamp: "%fullstamp%"

set "build=build%fullstamp%"
echo build: "%build%"

dotnet restore ..\src\Spreads.Core\Spreads.Core.csproj
dotnet pack ..\src\Spreads.Core\Spreads.Core.csproj -c RELEASE -o C:\tools\LocalNuget --version-suffix "%build%" 

dotnet restore ..\src\Spreads.Collections\Spreads.Collections.2017.fsproj
dotnet pack ..\src\Spreads.Collections\Spreads.Collections.2017.fsproj -c RELEASE -o C:\tools\LocalNuget --version-suffix "%build%"
rmdir /s /q ..\src\Spreads.Collections\obj

dotnet restore ..\src\Spreads\Spreads.2017.csproj
dotnet pack ..\src\Spreads\Spreads.2017.csproj -c RELEASE -o C:\tools\LocalNuget --version-suffix "%build%"

pause