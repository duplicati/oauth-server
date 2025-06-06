FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore OAuthServer.csproj -r linux-x64
# Build and publish a release
RUN dotnet publish OAuthServer.csproj -c Release -o out -r linux-x64 --self-contained false --no-restore

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim-amd64
WORKDIR /App
COPY --from=build-env /App/out .
ENV DOTNET_EnableDiagnostics=0
# Clear these to avoid unexpected bindings
ENV URLS=""
ENV ASPNETCORE_URLS=""
ENTRYPOINT ["dotnet", "OAuthServer.dll", "run"]