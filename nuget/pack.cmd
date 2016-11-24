dotnet restore ..\dotnetcore\Spreads.Core
dotnet pack ..\dotnetcore\Spreads.Core -c RELEASE -o ..\artifacts

dotnet restore ..\dotnetcore\Spreads.Collections
dotnet pack ..\dotnetcore\Spreads.Collections -c RELEASE -o ..\artifacts

dotnet restore ..\dotnetcore\Spreads
dotnet pack ..\dotnetcore\Spreads -c RELEASE -o ..\artifacts

dotnet restore ..\src\Spreads.Extensions
dotnet pack ..\dotnetcore\Spreads.Extensions -c RELEASE -o ..\artifacts

pause