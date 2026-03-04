# Build stage — context must be the solution root (not HttpTriggerDemo/)
# because Directory.Build.props and Directory.Packages.props live there.
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY HttpTriggerDemo/HttpTriggerDemo.csproj HttpTriggerDemo/
RUN dotnet restore HttpTriggerDemo/HttpTriggerDemo.csproj
COPY HttpTriggerDemo/ HttpTriggerDemo/
RUN dotnet publish HttpTriggerDemo/HttpTriggerDemo.csproj -c Release -o /app/publish

# Runtime stage
FROM --platform=linux/amd64 mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS runtime
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    AZURE_FUNCTIONS_ENVIRONMENT=Development \
    AzureWebJobsStorage=""
COPY --from=build /app/publish /home/site/wwwroot
