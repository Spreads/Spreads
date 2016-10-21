dotnet restore ..\src\Spreads.Core
dotnet pack ..\src\Spreads.Core -c RELEASE -o ..\artifacts

dotnet restore ..\src\Spreads.Collections
dotnet pack ..\src\Spreads.Collections -c RELEASE -o ..\artifacts

dotnet restore ..\src\Spreads
dotnet pack ..\src\Spreads -c RELEASE -o ..\artifacts

dotnet restore ..\src\Spreads.Extensions
dotnet pack ..\src\Spreads.Extensions -c RELEASE -o ..\artifacts

pause