FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 80

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY NuGet.config .
COPY build/common.props build/
COPY tests/Spreads.Core.Run/Spreads.Core.Run.csproj tests/Spreads.Core.Run/
COPY src/Spreads.Core/Spreads.Core.csproj src/Spreads.Core/
COPY tests/Spreads.Core.Tests/Spreads.Core.Tests.csproj tests/Spreads.Core.Tests/
COPY src/Spreads/Spreads.csproj src/Spreads/
COPY src/Spreads.Collections/Spreads.Collections.fsproj src/Spreads.Collections/
RUN dotnet restore tests/Spreads.Core.Run/Spreads.Core.Run.csproj
COPY . .
WORKDIR /src/tests/Spreads.Core.Run
RUN dotnet build Spreads.Core.Run.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish Spreads.Core.Run.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Spreads.Core.Run.dll"]
