# Multi-stage Dockerfile for ASP.NET Core application

# Build stage - Use SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy project file and restore dependencies
COPY DiagnosticScenarios/*.csproj ./DiagnosticScenarios/
RUN dotnet restore DiagnosticScenarios/DiagnosticScenarios.csproj

# Copy source code and build
COPY . ./
RUN dotnet publish DiagnosticScenarios/DiagnosticScenarios.csproj -c Release -o out

# Runtime stage - Use more compact runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/out .

# Expose port 80
EXPOSE 80

# Set environment variables for ASP.NET Core to listen on port 80
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "DiagnosticScenarios.dll"]
