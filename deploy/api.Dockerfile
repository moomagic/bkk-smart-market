# syntax=docker/dockerfile:1
# The API image for hosts that build one Dockerfile from the repository root (Render). Same build as api/Dockerfile,
# which docker compose keeps using; that one gets docs/rules through additional_contexts, which these hosts lack.
#   docker build -f deploy/api.Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY api/global.json api/Directory.Build.props api/BannedSymbols.txt api/SmartMarket.slnx ./
COPY api/src/SmartMarket.Domain/SmartMarket.Domain.csproj src/SmartMarket.Domain/
COPY api/src/SmartMarket.Application/SmartMarket.Application.csproj src/SmartMarket.Application/
COPY api/src/SmartMarket.Infrastructure/SmartMarket.Infrastructure.csproj src/SmartMarket.Infrastructure/
COPY api/src/SmartMarket.Api/SmartMarket.Api.csproj src/SmartMarket.Api/
RUN dotnet restore src/SmartMarket.Api/SmartMarket.Api.csproj
COPY api/src/ src/
RUN dotnet publish src/SmartMarket.Api/SmartMarket.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# The Vendor Assistant answers from these files only
COPY docs/rules/ docs/rules/
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartMarket.Api.dll"]
