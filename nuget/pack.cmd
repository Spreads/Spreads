dotnet restore ..\src\Spreads.Core\Spreads.Core.csproj
dotnet pack ..\src\Spreads.Core\Spreads.Core.csproj -c RELEASE -o ..\..\artifacts

dotnet restore ..\src\Spreads.Collections\Spreads.Collections.2017.fsproj
dotnet pack ..\src\Spreads.Collections\Spreads.Collections.2017.fsproj -c RELEASE -o ..\..\artifacts
rmdir /s /q ..\src\Spreads.Collections\obj

dotnet restore ..\src\Spreads\Spreads.csproj
dotnet pack ..\src\Spreads\Spreads.csproj -c RELEASE -o ..\..\artifacts

pause