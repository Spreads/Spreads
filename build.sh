#!/bin/bash
dotnet restore src/Spreads.Core/Spreads.Core.csproj
dotnet build src/Spreads.Core/Spreads.Core.csproj -f netstandard1.6 -c RELEASE

#dotnet restore src/Spreads.Collections/Spreads.Collections.2017.fsproj
#dotnet build src/Spreads.Collections/Spreads.Collections.2017.fsproj -c RELEASE

#dotnet restore src/Spreads/Spreads.2017.csproj
#dotnet build src/Spreads/Spreads.2017.csproj -c RELEASE

cd tests/Spreads.Core.xUnit
dotnet restore
dotnet test -f netcoreapp1.1
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi