@echo off
dotnet test tests/Spreads.Core.Tests/Spreads.Core.Tests.csproj -c Release  --filter TestCategory=CI -v m