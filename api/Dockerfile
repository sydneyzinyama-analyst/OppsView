# OppsView API container.
# Stage 1 builds with the full .NET SDK; stage 2 runs on the much smaller
# ASP.NET runtime image, so compilers and sources never ship.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore first, from the project file alone: Docker caches this layer, so
# changing code doesn't re-download packages.
COPY src/OppsView.Api/OppsView.Api.csproj src/OppsView.Api/
RUN dotnet restore src/OppsView.Api/OppsView.Api.csproj
COPY src/ src/
RUN dotnet publish src/OppsView.Api/OppsView.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# The .NET images include a non-root "app" user; run as it.
USER app
# ASP.NET Core in containers listens on 8080 by default.
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "OppsView.Api.dll"]
