dotnet restore ..\dotnetcore\Spreads.Core
dotnet pack ..\dotnetcore\Spreads.Core -c RELEASE -o ..\artifacts

dotnet restore ..\dotnetcore\Spreads.Collections
dotnet pack ..\dotnetcore\Spreads.Collections -c RELEASE -o ..\artifacts

dotnet restore ..\dotnetcore\Spreads
dotnet pack ..\dotnetcore\Spreads -c RELEASE -o ..\artifacts

pause