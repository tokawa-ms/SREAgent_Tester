English | [日本語](./Docker-README.md)

# DiagnosticScenarios Docker Setup

This project can be run as a Docker container.

## Requirements

- Docker Desktop or Docker Engine
- Docker Compose

## Build and Run

### Method 1: Using Docker Compose (Recommended)

```bash
# Build and start containers
docker-compose up --build

# Run in background
docker-compose up --build -d

# Stop containers
docker-compose down
```

### Method 2: Using Docker Commands Directly

```bash
# Build Docker image
docker build -t diagnostic-scenarios .

# Run container
docker run -p 8080:80 -p 8443:443 diagnostic-scenarios
```

## Access

Once the application is running, you can access it at the following URLs:

- HTTP: http://localhost:8080
- HTTPS: https://localhost:8443
- API Endpoint: http://localhost:8080/api/values

## Development Mode

If you want source code changes to be reflected immediately during development, you can create a `docker-compose.override.yml` and add the following settings:

```yaml
version: "3.8"
services:
  diagnosticscenarios:
    volumes:
      - .:/app/src
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
```

## Viewing Logs

```bash
# Display running logs
docker-compose logs -f

# Display logs for specific service only
docker-compose logs -f diagnosticscenarios
```

## Troubleshooting

- If ports 8080 or 8443 are already in use, change the ports setting in `docker-compose.yml`
- If SSL certificate errors occur, consider using HTTP only in development environment
