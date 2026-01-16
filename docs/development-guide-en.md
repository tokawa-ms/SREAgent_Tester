English | [日本語](./development-guide.md)

# Developer Guide

This document explains how to set up the development environment, customize, and debug SREAgent_Tester.

## Table of Contents

- [Development Environment Setup](#development-environment-setup)
- [Understanding Project Structure](#understanding-project-structure)
- [Build and Run](#build-and-run)
- [Adding New Scenarios](#adding-new-scenarios)
- [Debugging and Troubleshooting](#debugging-and-troubleshooting)
- [Dockerization](#dockerization)
- [Best Practices](#best-practices)

---

## Development Environment Setup

### Required Tools

1. **.NET 8 SDK**
   - Download from [official site](https://dotnet.microsoft.com/download/dotnet/8.0)
   - Verify installation:
     ```bash
     dotnet --version
     # Should display 8.0.x
     ```

2. **IDE (Optional)**
   - Visual Studio 2022 (Windows)
   - Visual Studio Code (All OS)
   - JetBrains Rider (All OS)

3. **Git**
   - Used for cloning repository

4. **Docker** (Optional)
   - Used for container execution and deployment

### Clone Repository

```bash
git clone https://github.com/tokawa-ms/SREAgent_Tester.git
cd SREAgent_Tester
```

### Restore Dependencies

```bash
dotnet restore
```

This restores the following NuGet packages:
- Microsoft.AspNetCore.App
- Microsoft.AspNetCore.Mvc.NewtonsoftJson
- Other required dependencies

---

## Understanding Project Structure

### Directory Layout

```
SREAgent_Tester/
├── DiagnosticScenarios/              # Main project
│   ├── Controllers/                  # MVC controllers
│   │   ├── DiagnosticScenarios.cs          # DiagScenarioController - Immediate execution API
│   │   ├── ScenarioToggleController.cs    # Toggle API
│   │   ├── HomeController.cs              # UI controller
│   │   └── ValuesController.cs            # Sample API
│   ├── Services/                     # Business logic
│   │   └── ScenarioToggleService.cs       # Scenario management
│   ├── Models/                       # Data models
│   │   └── ScenarioToggleModels.cs        # Request/Response models
│   ├── Views/                        # Razor views
│   │   ├── Home/
│   │   │   ├── Index.cshtml               # Immediate execution UI
│   │   │   └── ToggleScenarios.cshtml     # Toggle UI
│   │   └── Shared/                        # Common layouts
│   ├── wwwroot/                      # Static files
│   │   ├── css/
│   │   ├── js/
│   │   └── lib/                           # Client libraries
│   ├── Properties/
│   │   └── launchSettings.json            # Launch configuration
│   ├── appsettings.json              # Production config
│   ├── appsettings.Development.json  # Development config
│   ├── appsettings.Production.json   # Production config
│   ├── Program.cs                    # Entry point
│   ├── Startup.cs                    # Application configuration
│   └── DiagnosticScenarios.csproj    # Project file
├── docs/                             # Documentation
├── Dockerfile                        # Docker image definition
├── docker-compose.yml                # Docker Compose config
└── SREAgent_Tester.sln               # Solution file
```

### Key File Roles

**Program.cs**
- Application entry point
- Web host construction and startup

**Startup.cs**
- Dependency injection configuration (ConfigureServices)
- HTTP request pipeline configuration (Configure)

**Controllers/**
- Accept HTTP requests and return responses
- Delegate business logic to Service layer

**Services/**
- Business logic implementation
- Responsible for state management and complex processing

**Models/**
- Data structure definitions
- Request/Response schemas

---

## Build and Run

### Local Execution

#### Command Line

```bash
# Navigate to project directory
cd DiagnosticScenarios

# Build and run
dotnet run

# Or explicitly
dotnet build
dotnet run --project DiagnosticScenarios.csproj
```

Application starts at the following URLs:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001` (development only)

#### Visual Studio

1. Open `SREAgent_Tester.sln`
2. Press F5 to debug run
3. Or Ctrl+F5 for non-debug run

#### Visual Studio Code

1. Open folder
2. Press F5 to debug run
3. `.vscode/launch.json` is auto-generated

### Environment Variable Configuration

#### Development Environment

```bash
# Windows (PowerShell)
$env:ASPNETCORE_ENVIRONMENT="Development"

# Linux / macOS
export ASPNETCORE_ENVIRONMENT=Development
```

#### Production Environment

```bash
# Windows (PowerShell)
$env:ASPNETCORE_ENVIRONMENT="Production"
$env:ASPNETCORE_URLS="http://+:8080"

# Linux / macOS
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://+:8080
```

### Configuration File Priority

1. Environment variables
2. `appsettings.{Environment}.json`
3. `appsettings.json`

---

## Adding New Scenarios

### Adding Immediate Execution Scenario

**Steps:**

1. **Add method to DiagScenarioController (DiagnosticScenarios.cs)**

```csharp
/// <summary>
/// Scenario simulating network delay
/// </summary>
/// <param name="delayMs">Delay time (milliseconds)</param>
/// <returns>Success message</returns>
[HttpGet]
[Route("networkdelay/{delayMs:int}")]
public async Task<ActionResult<string>> NetworkDelay(int delayMs)
{
    if (delayMs < 0 || delayMs > 10000)
    {
        return BadRequest("delayMs must be between 0 and 10000.");
    }

    await Task.Delay(delayMs);
    
    return $"success:networkdelay ({delayMs}ms)";
}
```

2. **Update Documentation**

Add new endpoint to `docs/api-reference-en.md` and `docs/scenarios-en.md`

3. **Test**

```bash
curl http://localhost:5000/api/DiagScenario/networkdelay/1000
```

### Adding Toggle Scenario

More complex, explained step by step.

#### Step 1: Add to Enum

**Models/ScenarioToggleModels.cs:**

```csharp
public enum ScenarioToggleType
{
    ProbabilisticFailure,
    CpuSpike,
    MemoryLeak,
    ProbabilisticLatency,
    NetworkInstability  // New scenario
}
```

#### Step 2: Create Request Model

**Models/ScenarioToggleModels.cs:**

```csharp
/// <summary>
/// Network instability scenario configuration
/// </summary>
public sealed class NetworkInstabilityRequest
{
    /// <summary>Scenario duration (minutes)</summary>
    [Range(1, 180)]
    public int DurationMinutes { get; set; }

    /// <summary>Requests per second</summary>
    [Range(1, 1000)]
    public int RequestsPerSecond { get; set; }

    /// <summary>Timeout occurrence rate (%)</summary>
    [Range(0, 100)]
    public int TimeoutPercentage { get; set; }

    /// <summary>Timeout duration (milliseconds)</summary>
    [Range(1000, 30000)]
    public int TimeoutMilliseconds { get; set; }
}
```

#### Step 3: Add to Service Interface

**Services/ScenarioToggleService.cs:**

```csharp
public interface IScenarioToggleService
{
    // Existing methods...
    
    /// <summary>
    /// Starts network instability scenario
    /// </summary>
    Task<ScenarioStatus> StartNetworkInstabilityAsync(
        NetworkInstabilityRequest request, 
        CancellationToken cancellationToken);
}
```

#### Step 4: Add to Service Implementation

**Services/ScenarioToggleService.cs:**

```csharp
internal sealed class ScenarioToggleService : IScenarioToggleService
{
    // Existing code...

    public Task<ScenarioStatus> StartNetworkInstabilityAsync(
        NetworkInstabilityRequest request, 
        CancellationToken cancellationToken)
    {
        return StartScenarioAsync(
            ScenarioToggleType.NetworkInstability, 
            request.DurationMinutes, 
            request, 
            cancellationToken, 
            token => RunNetworkInstabilityAsync(request, token));
    }

    private async Task RunNetworkInstabilityAsync(
        NetworkInstabilityRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var windowStart = DateTime.UtcNow;
                var batch = new List<Task>(request.RequestsPerSecond);
                
                for (int i = 0; i < request.RequestsPerSecond; i++)
                {
                    batch.Add(SimulateNetworkRequestAsync(
                        request.TimeoutPercentage, 
                        request.TimeoutMilliseconds, 
                        cancellationToken));
                }

                await Task.WhenAll(batch).ConfigureAwait(false);

                var remaining = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - windowStart);
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected cancellation
        }
    }

    private async Task SimulateNetworkRequestAsync(
        int timeoutPercentage, 
        int timeoutMs, 
        CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        
        if (Random.Shared.Next(0, 100) < timeoutPercentage)
        {
            await Task.Delay(timeoutMs, cancellationToken).ConfigureAwait(false);
        }
    }
}
```

#### Step 5: Add Endpoint to Controller

**Controllers/ScenarioToggleController.cs:**

```csharp
[HttpPost("network-instability/start")]
public Task<ActionResult<ScenarioStatus>> StartNetworkInstability(
    [FromBody] NetworkInstabilityRequest request,
    CancellationToken cancellationToken) =>
    ExecuteStartAsync(() => _service.StartNetworkInstabilityAsync(request, cancellationToken));

[HttpPost("network-instability/stop")]
public ActionResult<ScenarioStatus> StopNetworkInstability()
{
    return Ok(_service.StopScenario(ScenarioToggleType.NetworkInstability));
}
```

#### Step 6: Test

```bash
# Start
curl -X POST http://localhost:5000/api/ScenarioToggle/network-instability/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":10,"requestsPerSecond":50,"timeoutPercentage":20,"timeoutMilliseconds":5000}'

# Check status
curl http://localhost:5000/api/ScenarioToggle/status

# Stop
curl -X POST http://localhost:5000/api/ScenarioToggle/network-instability/stop
```

---

## Debugging and Troubleshooting

### Enable Logging

**appsettings.Development.json:**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "DiagnosticScenarios": "Debug"
    }
  }
}
```

### Debugging with Visual Studio

1. Set breakpoints
2. Debug run with F5
3. Send request to stop at breakpoint

### View Logs

```bash
# Logs output to console
dotnet run
```

### Common Issues

#### Port Already in Use

**Error:**
```
Unable to bind to http://localhost:5000
```

**Solution:**
```bash
# Specify different port
dotnet run --urls "http://localhost:5001"
```

#### Out of Memory

**Symptom:** Application crashes with memory leak scenario

**Solution:**
- Reduce parameters
- For Docker, increase memory limit

```bash
docker run -m 2g sre-agent-tester
```

#### Timeout

**Symptom:** Timeout with long-running scenarios

**Solution:**
- Increase HTTP client timeout
- Adjust reverse proxy timeout

---

## Dockerization

### Build Docker Image

```bash
# At project root
docker build -t sre-agent-tester:latest .
```

### Run Container

```bash
docker run -d \
  --name sre-tester \
  -p 8080:8080 \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  sre-agent-tester:latest
```

### Using Docker Compose

```bash
docker-compose up -d
```

### Understanding Multi-Stage Build

`Dockerfile` uses multi-stage build:

1. **Build stage** - Build .NET app with SDK image
2. **Runtime stage** - Run with lightweight runtime image

This results in smaller final image size.

---

## Best Practices

### Coding Conventions

1. **XML Documentation Comments**
   - Add to all public methods
   - Describe parameters and return values

2. **Asynchronous Programming**
   - Always use `async/await` for I/O operations
   - Avoid `Task.Result` and `Task.Wait()`

3. **Error Handling**
   - Catch expected exceptions appropriately
   - Log unexpected exceptions

4. **Dependency Injection**
   - Use constructor injection
   - Depend on interfaces

### Performance

1. **Memory Management**
   - Ensure large objects are disposed with `using`
   - Don't retain references to unnecessary objects

2. **Thread Pool**
   - Execute CPU-intensive processing with `Task.Run`
   - Don't use thread pool for I/O operations

3. **Concurrency**
   - Use thread-safe collections like `ConcurrentDictionary`
   - Minimize `lock` usage

### Security

1. **Input Validation**
   - Validate all input parameters
   - Use `[Range]` attributes

2. **Access Control**
   - Use only in test environments
   - Restrict at network level

3. **Logging**
   - Don't log sensitive information
   - Use appropriate log levels

### Testing

#### Manual Testing

```bash
# Health check
curl http://localhost:5000/api/DiagScenario/taskasyncwait

# Status check
curl http://localhost:5000/api/ScenarioToggle/status
```

#### Automated Testing (Future Enhancement)

Recommended to add unit tests using xUnit in the future:

```csharp
[Fact]
public async Task StartCpuSpike_ShouldReturnOk()
{
    // Arrange
    var service = new ScenarioToggleService(logger);
    var request = new CpuSpikeRequest { /* ... */ };
    
    // Act
    var result = await service.StartCpuSpikeAsync(request, CancellationToken.None);
    
    // Assert
    Assert.True(result.IsActive);
}
```

---

## Next Steps

Ready to start development!

- Review existing scenarios: [scenarios-en.md](scenarios-en.md)
- Learn API usage: [api-reference-en.md](api-reference-en.md)
- Understand architecture: [architecture-en.md](architecture-en.md)

If you have questions or issues, please report them in GitHub Issues.
