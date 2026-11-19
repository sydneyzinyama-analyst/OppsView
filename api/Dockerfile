# OppsView API container, which also serves the website.
# Build it from the repository root (it needs both api/ and web/):
#   docker build -f api/Dockerfile -t oppsview-api .
#
# Stage 1 builds the React app; stage 2 builds the API with the full .NET
# SDK; stage 3 runs on the much smaller ASP.NET runtime image, so compilers,
# Node and sources never ship.

FROM node:22-slim AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
# The website calls the API on the same address, under /api.
ENV VITE_API_URL=/api
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore first, from the project file alone: Docker caches this layer, so
# changing code doesn't re-download packages.
COPY api/src/OppsView.Api/OppsView.Api.csproj src/OppsView.Api/
RUN dotnet restore src/OppsView.Api/OppsView.Api.csproj
COPY api/src/ src/
RUN dotnet publish src/OppsView.Api/OppsView.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
COPY --from=web /web/dist ./wwwroot
# The .NET images include a non-root "app" user; run as it.
USER app
# ASP.NET Core in containers listens on 8080 by default.
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production \
    Hosting__ApiPrefix=api
ENTRYPOINT ["dotnet", "OppsView.Api.dll"]
