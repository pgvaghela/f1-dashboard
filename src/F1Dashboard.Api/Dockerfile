# Multi-stage build for the F1Dashboard.Api (.NET 9) backend.
# Build context is this directory (src/F1Dashboard.Api).
#
# Deliberately avoids the mcr.microsoft.com/dotnet images: their registry
# (mcrprod.azurecr.io) rate-limits shared CI build hosts (429). Instead we use an
# Ubuntu base from Docker Hub and install .NET via the official dotnet-install
# script, which downloads from Microsoft's CDN rather than the throttled registry.

# ---------- build ----------
FROM ubuntu:24.04 AS build
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates libicu74 \
    && rm -rf /var/lib/apt/lists/*
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin \
    --channel 9.0 --install-dir /usr/share/dotnet
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="/usr/share/dotnet:${PATH}"

WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app --no-restore

# ---------- runtime ----------
FROM ubuntu:24.04 AS runtime
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates libicu74 \
    && rm -rf /var/lib/apt/lists/*
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin \
    --channel 9.0 --runtime aspnetcore --install-dir /usr/share/dotnet
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="/usr/share/dotnet:${PATH}"

WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_ENVIRONMENT=Production
# The app binds to $PORT at runtime (see Program.cs); EXPOSE is informational.
EXPOSE 8080

ENTRYPOINT ["dotnet", "F1Dashboard.Api.dll"]
