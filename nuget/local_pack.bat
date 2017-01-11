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

dotnet pack ..\dotnetcore\Spreads.Core -c DEBUG -o C:\tools\LocalNuget --version-suffix "%build%"

dotnet pack ..\dotnetcore\Spreads.Collections -c DEBUG -o C:\tools\LocalNuget --version-suffix "%build%"

dotnet pack ..\dotnetcore\Spreads -c DEBUG -o C:\tools\LocalNuget --version-suffix "%build%"

dotnet pack ..\dotnetcore\Spreads.Extensions -c DEBUG -o C:\tools\LocalNuget --version-suffix "%build%"

pause