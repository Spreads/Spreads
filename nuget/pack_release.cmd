del ..\artifacts\*.nupkg

dotnet restore ..\src\Spreads.Core\Spreads.Core.csproj
dotnet pack ..\src\Spreads.Core\Spreads.Core.csproj -c Release -o ..\artifacts -p:AutoSuffix=False

REM dotnet restore ..\src\Spreads\Spreads.csproj
REM dotnet pack ..\src\Spreads\Spreads.csproj -c Release -o ..\artifacts -p:AutoSuffix=False

pause