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

dotnet test ..\tests\Spreads.Core.Tests\Spreads.Core.Tests.csproj -c RELEASE  --filter TestCategory=CI -v n

dotnet restore ..\src\Spreads.Core\Spreads.Core.csproj
dotnet pack ..\src\Spreads.Core\Spreads.Core.csproj -c RELEASE -o C:\tools\LocalNuget --version-suffix "%build%"R

dotnet restore ..\src\Spreads.Collections\Spreads.Collections.fsproj
dotnet pack ..\src\Spreads.Collections\Spreads.Collections.fsproj -c RELEASE -o C:\tools\LocalNuget --version-suffix "%build%"R

dotnet restore ..\src\Spreads\Spreads.csproj
dotnet pack ..\src\Spreads\Spreads.csproj -c RELEASE -o C:\tools\LocalNuget --version-suffix "%build%"R

@for %%f in (C:\tools\LocalNuget\*"%build%"R.nupkg) do @C:\tools\nuget\NuGet.exe push %%f -source https://www.nuget.org/api/v2/package

pause