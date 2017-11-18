dotnet restore src/Spreads.Core/Spreads.Core.csproj
dotnet build src/Spreads.Core/Spreads.Core.csproj -c RELEASE

dotnet restore src/Spreads.Collections/Spreads.Collections.fsproj
dotnet build src/Spreads.Collections/Spreads.Collections.fsproj -c RELEASE

dotnet restore src/Spreads/Spreads.csproj
dotnet build src/Spreads/Spreads.csproj -c RELEASE
