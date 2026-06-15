# Multi-stage build for the F1Dashboard.Api (.NET 9) backend.
# Build context is this directory (src/F1Dashboard.Api).

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first (cached unless the csproj changes), then publish.
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_ENVIRONMENT=Production
# The app binds to $PORT at runtime (see Program.cs); EXPOSE is informational.
EXPOSE 8080

ENTRYPOINT ["dotnet", "F1Dashboard.Api.dll"]
