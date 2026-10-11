# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props BannedSymbols.txt SmartMarket.slnx ./
COPY src/SmartMarket.Domain/SmartMarket.Domain.csproj src/SmartMarket.Domain/
COPY src/SmartMarket.Application/SmartMarket.Application.csproj src/SmartMarket.Application/
COPY src/SmartMarket.Infrastructure/SmartMarket.Infrastructure.csproj src/SmartMarket.Infrastructure/
COPY src/SmartMarket.Api/SmartMarket.Api.csproj src/SmartMarket.Api/
RUN dotnet restore src/SmartMarket.Api/SmartMarket.Api.csproj
COPY src/ src/
RUN dotnet publish src/SmartMarket.Api/SmartMarket.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# "rules" is docs/rules, passed by docker compose (additional_contexts, Compose ≥ 2.17); a bare `docker build ./api` lacks it.
COPY --from=rules . docs/rules/
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartMarket.Api.dll"]
