# syntax=docker/dockerfile:1
# Build the NMate API image.
# Run from repo root: docker build -f src/SUPPORT.Api/Dockerfile -t support-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore layer: only the files that affect package resolution, so a code change
# does not invalidate the NuGet cache layer.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/SUPPORT.Domain/SUPPORT.Domain.csproj                 src/SUPPORT.Domain/
COPY src/SUPPORT.Application/SUPPORT.Application.csproj       src/SUPPORT.Application/
COPY src/SUPPORT.Infrastructure/SUPPORT.Infrastructure.csproj src/SUPPORT.Infrastructure/
COPY src/SUPPORT.Api/SUPPORT.Api.csproj                       src/SUPPORT.Api/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/SUPPORT.Api/SUPPORT.Api.csproj
COPY src/ src/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/SUPPORT.Api/SUPPORT.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# libgssapi-krb5-2: Npgsql probes GSSAPI at connect time and logs a warning without it.
# curl: used by the compose healthcheck (the aspnet image ships neither curl nor wget).
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Knowledge files are mounted read-only here in production (Knowledge__Sources__dashboard).
VOLUME ["/knowledge"]
USER $APP_UID
ENTRYPOINT ["dotnet", "SUPPORT.Api.dll"]
