# Build the worker, then publish a small runtime-only image. Container Apps
# pulls this image and runs it under a KEDA queue scaler.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy the centrally managed package + framework versions and the project files
# first so docker layer caching survives source-only changes.
COPY ../Directory.Build.props ./Directory.Build.props
COPY ../Directory.Packages.props ./Directory.Packages.props
COPY ../global.json ./global.json
COPY ./Settlement.Core/Settlement.Core.csproj ./Settlement.Core/
COPY ./Settlement.ContainerApp/Settlement.ContainerApp.csproj ./Settlement.ContainerApp/
RUN dotnet restore ./Settlement.ContainerApp/Settlement.ContainerApp.csproj

COPY ./Settlement.Core ./Settlement.Core
COPY ./Settlement.ContainerApp ./Settlement.ContainerApp
RUN dotnet publish ./Settlement.ContainerApp/Settlement.ContainerApp.csproj \
    -c Release \
    -o /app \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app .

# Run as non-root. Container Apps does not enforce this, but smaller blast
# radius is a small win.
RUN addgroup -S settle && adduser -S settle -G settle
USER settle

ENTRYPOINT ["dotnet", "Settlement.ContainerApp.dll"]
