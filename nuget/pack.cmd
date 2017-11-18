dotnet restore ..\src\Spreads.Core\Spreads.Core.csproj
dotnet pack ..\src\Spreads.Core\Spreads.Core.csproj -c RELEASE -o ..\..\artifacts

dotnet restore ..\src\Spreads.Collections\Spreads.Collections.fsproj
dotnet pack ..\src\Spreads.Collections\Spreads.Collections.fsproj -c RELEASE -o ..\..\artifacts

dotnet restore ..\src\Spreads\Spreads.csproj
dotnet pack ..\src\Spreads\Spreads.csproj -c RELEASE -o ..\..\artifacts

pause