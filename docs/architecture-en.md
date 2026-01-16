English | [日本語](./architecture.md)

# Architecture Overview

This document explains the overall architecture and design philosophy of SREAgent_Tester.

## Purpose

SREAgent_Tester is a diagnostic test application designed to verify SRE (Site Reliability Engineering) tools and monitoring systems. By intentionally generating various failure scenarios and load patterns, you can confirm that monitoring tools, tracing systems, and diagnostic tools work correctly.

## Technology Stack

- **Framework**: ASP.NET Core 8.0
- **Language**: C# 12
- **Architecture Pattern**: MVC (Model-View-Controller)
- **Dependency Injection**: ASP.NET Core standard DI container
- **Logging**: Microsoft.Extensions.Logging
- **JSON Serialization**: Newtonsoft.Json

## Project Structure

```
SREAgent_Tester/
├── DiagnosticScenarios/          # Main application
│   ├── Controllers/              # API endpoints
│   │   ├── DiagnosticScenarios.cs          # DiagScenarioController - Immediate execution scenarios
│   │   ├── ScenarioToggleController.cs    # Toggle scenarios
│   │   └── HomeController.cs              # UI controller
│   ├── Services/                 # Business logic
│   │   └── ScenarioToggleService.cs       # Scenario management service
│   ├── Models/                   # Data models
│   │   └── ScenarioToggleModels.cs        # Scenario configuration models
│   ├── Views/                    # Razor views (UI)
│   ├── wwwroot/                  # Static files
│   ├── Program.cs                # Entry point
│   └── Startup.cs                # Application configuration
├── docs/                         # Documentation
└── Docker related files
```

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                    Client                                │
│  (Browser / curl / Test Tools / SRE Agent)              │
└────────────────┬────────────────────────────────────────┘
                 │ HTTP/HTTPS
                 ▼
┌─────────────────────────────────────────────────────────┐
│              ASP.NET Core Middleware                     │
│  (Routing / Auth / Static Files / Error Handling)       │
└────────────────┬────────────────────────────────────────┘
                 │
        ┌────────┴────────┐
        │                 │
        ▼                 ▼
┌──────────────┐  ┌──────────────────┐
│ HomeController│  │ API Controllers  │
│  (Web UI)    │  │                  │
└──────────────┘  └────────┬─────────┘
                           │
              ┌────────────┴────────────┐
              │                         │
              ▼                         ▼
    ┌──────────────────┐    ┌─────────────────────┐
    │ DiagScenario     │    │ ScenarioToggle      │
    │ Controller       │    │ Controller          │
    │ (Immediate)      │    │ (Background)        │
    └──────────────────┘    └──────────┬──────────┘
                                       │
                                       ▼
                            ┌──────────────────────┐
                            │ ScenarioToggle       │
                            │ Service              │
                            │ (State & Execution)  │
                            └──────────────────────┘
```

## Two Scenario Types

### 1. Immediate Execution Scenarios (DiagScenarioController)

**Features:**
- Execute immediately upon receiving API request
- Do not return response until completion
- Ideal for one-shot diagnostic tests

**Use Cases:**
- Testing deadlock detection tools
- Immediate CPU spike generation
- Short-term memory spike tests
- Exception tracing tests
- Task waiting pattern comparison

**Endpoint Examples:**
- `GET /api/DiagScenario/deadlock`
- `GET /api/DiagScenario/highcpu/5000`
- `GET /api/DiagScenario/exception`

### 2. Toggle Scenarios (ScenarioToggleController + Service)

**Features:**
- Control start/stop with separate APIs
- Long-running execution as background tasks
- Can run multiple scenarios concurrently
- Real-time status checking

**Use Cases:**
- Long-term load testing
- Continuous fault injection
- Reproducing probabilistic issues
- Continuous SRE agent testing

**Endpoint Examples:**
- `POST /api/ScenarioToggle/cpu-spike/start`
- `POST /api/ScenarioToggle/cpu-spike/stop`
- `GET /api/ScenarioToggle/status`

## Core Design Patterns

### Dependency Injection (DI)

All services and controllers use the dependency injection pattern.

```csharp
// Register service in Startup.cs
services.AddSingleton<IScenarioToggleService, ScenarioToggleService>();

// Inject service in controller
public ScenarioToggleController(IScenarioToggleService service)
{
    _service = service;
}
```

**Advantages:**
- Easy to test (can replace with mocks)
- Loosely coupled code
- Automatic lifetime management

### Singleton Service

`ScenarioToggleService` is registered as a singleton, with a single instance shared across the application. This allows scenario state to be maintained across multiple requests.

### Thread-Safe State Management

Background task state is managed thread-safely using `ConcurrentDictionary` and `lock`.

```csharp
private readonly ConcurrentDictionary<ScenarioToggleType, ScenarioState> _state;

lock (state.SyncRoot)
{
    // Safe state update
}
```

### Asynchronous Programming (async/await)

All I/O operations and long-running processes use asynchronous patterns.

```csharp
public async Task<ActionResult<string>> MemSpike(int seconds, CancellationToken cancellationToken)
{
    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
}
```

**Advantages:**
- Prevents thread pool exhaustion
- High scalability
- Can handle more concurrent requests

### Cancellation Token Support

All long-running processes support `CancellationToken` for graceful shutdown.

## Data Flow

### Toggle Scenario Start Flow

```
1. Client calls POST /api/ScenarioToggle/cpu-spike/start
   ↓
2. ScenarioToggleController receives request
   ↓
3. Deserialize CpuSpikeRequest from request body
   ↓
4. Call ScenarioToggleService.StartCpuSpikeAsync()
   ↓
5. Service checks state (throws exception if already running)
   ↓
6. Start background task
   ↓
7. Return scenario status (200 OK)
   ↓
8. Background task runs until specified time
   ↓
9. Task completes and updates state
```

### Status Check Flow

```
1. Client calls GET /api/ScenarioToggle/status
   ↓
2. ScenarioToggleController receives request
   ↓
3. Call ScenarioToggleService.GetStatuses()
   ↓
4. Get current state of all scenarios
   ↓
5. Return in JSON format (running status, scheduled end time, config, etc.)
```

## Scalability Considerations

### Single Instance Design

Current implementation assumes single instance execution. When horizontally scaling to multiple instances, note the following:

- Scenario state is managed locally per instance
- Session affinity required when using load balancer
- Consider external state store (Redis, etc.) for distributed environments

### Resource Management

- Memory leak scenarios intentionally hold memory; be aware of container/VM memory limits
- CPU spike scenarios may use all cores
- Running multiple scenarios simultaneously may cause resource contention

## Security Considerations

**⚠️ Important Warning:**

This application is **for test/verification environments only**. Never run in production environment.

**Reasons:**
- Intentionally exhausts system resources
- Causes deadlocks and exceptions
- Triggers memory leaks
- No authentication/authorization mechanisms

**Recommendations:**
- Restrict access at network level
- Run in dedicated isolated environment
- Set resource limits (CPU, memory)
- Restart application regularly

## Extensibility

### Adding New Scenarios

Steps to add new toggle scenario:

1. Add new value to `ScenarioToggleType` enum
2. Create request model class (with range validation)
3. Add method signature to `IScenarioToggleService`
4. Add implementation to `ScenarioToggleService`
5. Add endpoint to `ScenarioToggleController`
6. Update documentation

Steps to add new immediate execution scenario:

1. Add new action method to `DiagScenarioController` (DiagnosticScenarios.cs)
2. Set route attributes
3. Implement parameter validation
4. Update documentation

## Monitoring and Logging

### Log Levels

- **Information**: Scenario start/completion, important state changes
- **Warning**: Expected exceptions (probabilistic failures, etc.)
- **Error**: Unexpected exceptions, scenario failures

### Recommended Monitoring Metrics

- CPU usage (verifying CPU spike scenarios)
- Memory usage (verifying memory leak scenarios)
- Thread pool statistics (verifying task wait scenarios)
- Exception rate (verifying exception scenarios)
- Response time (verifying latency scenarios)

## Summary

SREAgent_Tester is a simple yet powerful diagnostic tool. Two different scenario types support both immediate tests and long-term load tests. The extensible design makes it easy to add new scenarios.

Next Steps:
- [API Reference](api-reference-en.md) - Details of all endpoints
- [Developer Guide](development-guide-en.md) - Development and customization methods
- [Scenario List](scenarios-en.md) - Details of available scenarios
