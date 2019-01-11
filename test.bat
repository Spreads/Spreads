@echo off
dotnet test tests/Spreads.Core.Tests/Spreads.Core.Tests.csproj -f netcoreapp3.0 -c Release  --filter TestCategory=CI -v n
dotnet test tests/Spreads.Core.Tests/Spreads.Core.Tests.csproj -f net461 -c Release  --filter TestCategory=CI -v n
