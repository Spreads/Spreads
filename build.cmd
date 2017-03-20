dotnet restore src/Spreads.Core/Spreads.Core.csproj
dotnet build src/Spreads.Core/Spreads.Core.csproj -c RELEASE

dotnet restore src/Spreads.Collections/Spreads.Collections.2017.fsproj
dotnet build src/Spreads.Collections/Spreads.Collections.2017.fsproj -c RELEASE
rmdir /s /q src/Spreads.Collections/obj

dotnet restore src/Spreads/Spreads.2017.csproj
dotnet build src/Spreads/Spreads.2017.csproj -c RELEASE
