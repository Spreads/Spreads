del ..\artifacts\*.nupkg

dotnet restore ..\src\Spreads.Core\Spreads.Core.csproj
dotnet pack ..\src\Spreads.Core\Spreads.Core.csproj -c Release -o ..\artifacts -p:AutoSuffix=True

dotnet restore ..\src\Spreads.Collections\Spreads.Collections.fsproj
dotnet pack ..\src\Spreads.Collections\Spreads.Collections.fsproj -c Release -o ..\artifacts -p:AutoSuffix=True

dotnet restore ..\src\Spreads\Spreads.csproj
dotnet pack ..\src\Spreads\Spreads.csproj -c Release -o ..\artifacts -p:AutoSuffix=True

pause