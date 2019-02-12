@echo off
dotnet test tests/Spreads.Core.Tests/Spreads.Core.Tests.csproj -f netcoreapp3.0 -c Debug  --filter TestCategory=CI -v n

