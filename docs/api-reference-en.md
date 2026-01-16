English | [日本語](./api-reference.md)

# API Reference

This document describes all API endpoints provided by SREAgent_Tester in detail.

## Table of Contents

- [Immediate Execution Scenario API](#immediate-execution-scenario-api)
- [External Load Tool API](#external-load-tool-api)
- [Toggle Scenario API](#toggle-scenario-api)
- [Common Specifications](#common-specifications)

---

## Immediate Execution Scenario API

Base URL: `/api/DiagScenario`

These endpoints execute scenarios immediately upon receiving requests and do not return responses until completion.

### Deadlock

Intentionally causes a deadlock.

**Endpoint:** `GET /api/DiagScenario/deadlock`

**Parameters:** None

**Response:**
- On success: `200 OK` - `"success:deadlock"`
- Note: Requests may hang when deadlock occurs

**Usage Example:**
```bash
curl http://localhost:5000/api/DiagScenario/deadlock
```

**Description:**
- Causes deadlock by having multiple threads wait for each other's locks
- Used for testing deadlock detection tools and thread dump analysis

---

### High CPU Usage

Executes CPU busy loop for the specified time.

**Endpoint:** `GET /api/DiagScenario/highcpu/{milliseconds}`

**Path Parameters:**
- `milliseconds` (integer): Busy loop execution time (milliseconds)
  - Recommended range: 100～30000

**Response:**
- On success: `200 OK` - `"success:highcpu"`

**Usage Example:**
```bash
# Increase CPU usage for 5 seconds
curl http://localhost:5000/api/DiagScenario/highcpu/5000
```

**Description:**
- Executes CPU-intensive busy loop
- Ideal for testing CPU monitoring tools and performance profilers

---

### Memory Spike

Periodically allocates and releases large amounts of memory to rapidly fluctuate memory usage.

**Endpoint:** `GET /api/DiagScenario/memspike/{seconds}`

**Path Parameters:**
- `seconds` (integer): Scenario execution time (seconds)
  - Range: 1～1800

**Response:**
- On success: `200 OK` - `"success:memspike"`
- On error: `400 Bad Request` - `"seconds must be between 1 and 1800."`

**Usage Example:**
```bash
# Generate memory spike for 60 seconds
curl http://localhost:5000/api/DiagScenario/memspike/60
```

**Description:**
- Allocates memory at 5-second intervals, then releases it repeatedly via GC
- Used for testing memory profilers and GC monitoring tools

---

### Memory Leak

Allocates memory of the specified size and retains it without releasing.

**Endpoint:** `GET /api/DiagScenario/memleak/{kilobytes}`

**Path Parameters:**
- `kilobytes` (integer): Memory size to allocate (KB)
  - Range: 1～10240

**Response:**
- On success: `200 OK` - `"success:memleak ({kilobytes}KB retained)"`
- On error: `400 Bad Request` - `"kilobytes must be between 1 and 10240."`

**Usage Example:**
```bash
# Cause 1MB memory leak
curl http://localhost:5000/api/DiagScenario/memleak/1024
```

**Description:**
- Allocated memory is held in a static field and not released until application restart
- Multiple calls accumulate memory leaks
- Used for testing memory leak detection tools

---

### Exception

Throws a simple exception.

**Endpoint:** `GET /api/DiagScenario/exception`

**Parameters:** None

**Response:**
- Always: `500 Internal Server Error` - Exception message

**Usage Example:**
```bash
curl http://localhost:5000/api/DiagScenario/exception
```

**Description:**
- Used for testing error handling, logging, and tracing systems

---

### Exception Burst

Generates exceptions at high frequency.

**Endpoint:** `GET /api/DiagScenario/exceptionburst/{durationSeconds}/{exceptionsPerSecond}`

**Path Parameters:**
- `durationSeconds` (integer): Execution time (seconds)
  - Range: 1～1800
- `exceptionsPerSecond` (integer): Number of exceptions per second
  - Range: 1～1000

**Response:**
- On success: `200 OK` - `"success:exceptionburst ({total} exceptions generated)"`
- On error: `400 Bad Request` - Parameter error message

**Usage Example:**
```bash
# Generate 10 exceptions per second for 30 seconds
curl http://localhost:5000/api/DiagScenario/exceptionburst/30/10
```

**Description:**
- All exceptions are caught and logged
- Used for error log and trace load testing

---

### Probabilistic Load

Simulates backend requests with probabilistic exception generation.

**Endpoint:** `GET /api/DiagScenario/probabilisticload/{durationSeconds}/{requestsPerSecond}/{exceptionPercentage}`

**Path Parameters:**
- `durationSeconds` (integer): Execution time (seconds)
  - Range: 1～1800
- `requestsPerSecond` (integer): Requests per second
  - Range: 1～1000
- `exceptionPercentage` (integer): Exception rate (%)
  - Range: 0～100

**Response:**
- On success: `200 OK` - `"success:probabilisticload (durationSeconds=X, totalRequests=Y, successes=Z, failures=W)"`
- On error: `400 Bad Request` - Parameter error message

**Usage Example:**
```bash
# 60 seconds, 50 requests/sec, 20% failure rate
curl http://localhost:5000/api/DiagScenario/probabilisticload/60/50/20
```

**Description:**
- Each request simulates 500ms delay
- Generates exceptions at specified probability
- Used for load testing and error rate monitoring tests

---

### Task Wait Pattern (Task.Wait)

**Anti-pattern** - Blocking wait using Task.Result/Task.Wait()

**Endpoint:** `GET /api/DiagScenario/taskwait`

**Parameters:** None

**Response:**
- On success: `200 OK` - `"success:taskwait"`

**Usage Example:**
```bash
curl http://localhost:5000/api/DiagScenario/taskwait
```

**Description:**
- Problematic pattern that can cause thread pool exhaustion
- Used as educational material for performance issue diagnosis

---

### Task Wait Pattern (Thread.Sleep)

**Anti-pattern** - Spin loop waiting for task completion with Thread.Sleep()

**Endpoint:** `GET /api/DiagScenario/tasksleepwait`

**Parameters:** None

**Response:**
- On success: `200 OK` - `"success:tasksleepwait"`

**Usage Example:**
```bash
curl http://localhost:5000/api/DiagScenario/tasksleepwait
```

**Description:**
- Problematic pattern that wastes threads
- Used as educational material for performance issue diagnosis

---

### Task Wait Pattern (async/await)

**Recommended pattern** - Non-blocking wait using async/await

**Endpoint:** `GET /api/DiagScenario/taskasyncwait`

**Parameters:** None

**Response:**
- On success: `200 OK` - `"success:taskasyncwait"`

**Usage Example:**
```bash
curl http://localhost:5000/api/DiagScenario/taskasyncwait
```

**Description:**
- Correct pattern that doesn't block threads
- Used for comparison with other taskwait endpoints

---

## External Load Tool API

Base URL: `/api/DirectTest`

These endpoints are designed to be called directly from external load testing tools like JMeter or Apache Bench. All use GET method with query parameters for easy integration with load testing tools.

### OpenAPI/Swagger Documentation

Detailed API schema can be accessed at the following URLs when the application is running:
- Swagger UI: `http://localhost:5000/swagger`
- OpenAPI JSON: `http://localhost:5000/swagger/v1/swagger.json`

### Random Response Latency

Generates random delay from 0 to specified value. Ideal for simulating latency variation.

**Endpoint:** `GET /api/DirectTest/RandomLatency`

**Query Parameters:**
- `maxLatencyInMilliSeconds` (integer): Maximum delay time (milliseconds)
  - Range: 0～30000

**Response:**
- On success: `200 OK` - `"success:randomlatency (max=XXXms, actual=YYYms)"`
- On error: `400 Bad Request` - `"maxLatencyInMilliSeconds must be between 0 and 30000."`

**Usage Example:**
```bash
# Random delay up to 1 second
curl "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000"

# For JMeter
# HTTP Request Sampler
# Protocol: http
# Server Name: localhost
# Port: 5000
# Path: /api/DirectTest/RandomLatency
# Parameters:
#   - Name: maxLatencyInMilliSeconds, Value: 1000
```

**Description:**
- Actual delay time is randomly determined between 0 and maxLatencyInMilliSeconds
- Response includes actual delay time
- Useful for load tests observing latency distribution

---

### Random Exception

Generates exceptions at specified probability and returns 500 error. Ideal for error rate simulation.

**Endpoint:** `GET /api/DirectTest/RandomException`

**Query Parameters:**
- `exceptionPercentage` (integer): Exception probability (percent)
  - Range: 0～100

**Response:**
- On success: `200 OK` - `"success:randomexception (exceptionPercentage=XX%, no exception)"`
- On exception: `500 Internal Server Error` - Error message
- On error: `400 Bad Request` - `"exceptionPercentage must be between 0 and 100."`

**Usage Example:**
```bash
# 10% probability of exception
curl "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=10"

# Always succeed (no error)
curl "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=0"

# Always fail (500 error)
curl "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=100"
```

**Description:**
- Generates exceptions probabilistically using random numbers
- Used for error rate monitoring and SLO calculation verification
- Returns 500 Internal Server Error when exception occurs

---

### Memory Allocation

Allocates and holds memory of specified size for specified time. Simulates increased memory usage.

**Endpoint:** `GET /api/DirectTest/HighMem`

**Query Parameters:**
- `secondsToKeepMem` (integer): Seconds to hold memory
  - Range: 1～300
- `keepMemSize` (integer): Memory size to allocate (MB)
  - Range: 1～2048

**Response:**
- On success: `200 OK` - `"success:highmem (kept XXXmb for YYY seconds)"`
- On error: `400 Bad Request` - Parameter error message

**Usage Example:**
```bash
# Hold 100MB for 10 seconds
curl "http://localhost:5000/api/DirectTest/HighMem?secondsToKeepMem=10&keepMemSize=100"

# Hold 1GB for 30 seconds (high load)
curl "http://localhost:5000/api/DirectTest/HighMem?secondsToKeepMem=30&keepMemSize=1024"
```

**Description:**
- Allocates memory using byte arrays
- Memory is automatically released after specified time
- Used for testing memory monitoring tools and alerts
- Caution: Be careful not to exceed your environment's memory capacity

---

### High CPU Load

Executes CPU busy loop for specified time to increase CPU usage.

**Endpoint:** `GET /api/DirectTest/HighCPU`

**Query Parameters:**
- `millisecondsToKeepHighCPU` (integer): Milliseconds to maintain high CPU load
  - Range: 100～60000

**Response:**
- On success: `200 OK` - `"success:highcpu (ran for XXXms)"`
- On error: `400 Bad Request` - `"millisecondsToKeepHighCPU must be between 100 and 60000."`

**Usage Example:**
```bash
# High CPU load for 5 seconds
curl "http://localhost:5000/api/DirectTest/HighCPU?millisecondsToKeepHighCPU=5000"

# High CPU load for 30 seconds
curl "http://localhost:5000/api/DirectTest/HighCPU?millisecondsToKeepHighCPU=30000"
```

**Description:**
- Executes CPU busy loop to occupy CPU
- Used for testing CPU usage monitoring tools and performance alerts
- Actual execution time is included in response

---

### Load Testing Tool Usage Examples

#### Using with JMeter

**Thread Group Configuration:**
```
Number of Threads: 100
Ramp-Up Period: 10
Loop Count: 100
```

**HTTP Request Sampler 1 - Random Latency:**
```
Protocol: http
Server Name: localhost
Port: 5000
Path: /api/DirectTest/RandomLatency
Parameters:
  - maxLatencyInMilliSeconds: 2000
```

**HTTP Request Sampler 2 - Random Exception:**
```
Protocol: http
Server Name: localhost
Port: 5000
Path: /api/DirectTest/RandomException
Parameters:
  - exceptionPercentage: 5
```

#### Using with Apache Bench

```bash
# 100 parallel, 1000 requests
ab -n 1000 -c 100 "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=500"

# Test with 5% error rate
ab -n 1000 -c 50 "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=5"
```

#### Simple Load Test with curl

```bash
# Execute 10 requests in parallel
for i in {1..10}; do
  curl "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000" &
done
wait
```

---

## Toggle Scenario API

Base URL: `/api/ScenarioToggle`

These endpoints control long-running scenarios that execute as background tasks.

### Get All Scenario Status

Gets current status of all toggle scenarios.

**Endpoint:** `GET /api/ScenarioToggle/status`

**Parameters:** None

**Response:**
- On success: `200 OK` - Array of scenario statuses

**Response Example:**
```json
[
  {
    "scenario": "ProbabilisticFailure",
    "isActive": true,
    "endsAtUtc": "2024-01-15T12:30:00Z",
    "lastMessage": "Running",
    "activeConfig": {
      "durationMinutes": 30,
      "requestsPerSecond": 100,
      "failurePercentage": 10
    }
  },
  {
    "scenario": "CpuSpike",
    "isActive": false,
    "endsAtUtc": null,
    "lastMessage": "Scenario finished",
    "activeConfig": null
  }
]
```

**Usage Example:**
```bash
curl http://localhost:5000/api/ScenarioToggle/status
```

---

### Probabilistic Failure Scenario

#### Start

**Endpoint:** `POST /api/ScenarioToggle/probabilistic-failure/start`

**Request Body:**
```json
{
  "durationMinutes": 30,
  "requestsPerSecond": 100,
  "failurePercentage": 10
}
```

**Parameters:**
- `durationMinutes` (integer): Execution time (minutes)
  - Range: 1～180
- `requestsPerSecond` (integer): Requests per second
  - Range: 1～1000
- `failurePercentage` (integer): Failure rate (%)
  - Range: 0～100

**Response:**
- On success: `200 OK` - Scenario status
- Already running: `409 Conflict` - Error message

**Usage Example:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/probabilistic-failure/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":30,"requestsPerSecond":100,"failurePercentage":10}'
```

#### Stop

**Endpoint:** `POST /api/ScenarioToggle/probabilistic-failure/stop`

**Parameters:** None

**Response:**
- On success: `200 OK` - Scenario status

**Usage Example:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/probabilistic-failure/stop
```

---

### CPU Spike Scenario

#### Start

**Endpoint:** `POST /api/ScenarioToggle/cpu-spike/start`

**Request Body:**
```json
{
  "durationMinutes": 60,
  "intervalSeconds": 30,
  "triggerPercentage": 50,
  "spikeSeconds": 10
}
```

**Parameters:**
- `durationMinutes` (integer): Execution time (minutes)
  - Range: 1～180
- `intervalSeconds` (integer): Check interval (seconds)
  - Range: 1～300
- `triggerPercentage` (integer): Spike occurrence probability (%)
  - Range: 0～100
- `spikeSeconds` (integer): Spike duration (seconds)
  - Range: 1～30

**Response:**
- On success: `200 OK` - Scenario status
- Already running: `409 Conflict` - Error message

**Usage Example:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/cpu-spike/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":60,"intervalSeconds":30,"triggerPercentage":50,"spikeSeconds":10}'
```

#### Stop

**Endpoint:** `POST /api/ScenarioToggle/cpu-spike/stop`

**Parameters:** None

**Response:**
- On success: `200 OK` - Scenario status

**Usage Example:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/cpu-spike/stop
```

---

### Memory Leak Scenario

#### Start

**Endpoint:** `POST /api/ScenarioToggle/memory-leak/start`

**Request Body:**
```json
{
  "durationMinutes": 60,
  "intervalSeconds": 60,
  "triggerPercentage": 80,
  "memoryMegabytes": 100,
  "holdSeconds": 30
}
```

**Parameters:**
- `durationMinutes` (integer): Execution time (minutes)
  - Range: 1～180
- `intervalSeconds` (integer): Check interval (seconds)
  - Range: 1～300
- `triggerPercentage` (integer): Memory allocation probability (%)
  - Range: 0～100
- `memoryMegabytes` (integer): Allocation size (MB)
  - Range: 1～1024
- `holdSeconds` (integer): Hold time (seconds)
  - Range: 1～60

**Response:**
- On success: `200 OK` - Scenario status
- Already running: `409 Conflict` - Error message

**Usage Example:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/memory-leak/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":60,"intervalSeconds":60,"triggerPercentage":80,"memoryMegabytes":100,"holdSeconds":30}'
```

#### Stop

**Endpoint:** `POST /api/ScenarioToggle/memory-leak/stop`

**Parameters:** None

**Response:**
- On success: `200 OK` - Scenario status

**Description:**
- All allocated memory is automatically released when stopped

**Usage Example:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/memory-leak/stop
```

---

### Probabilistic Latency Scenario

#### Start

**Endpoint:** `POST /api/ScenarioToggle/probabilistic-latency/start`

**Request Body:**
```json
{
  "durationMinutes": 30,
  "requestsPerSecond": 50,
  "triggerPercentage": 20,
  "delayMilliseconds": 1000
}
```

**Parameters:**
- `durationMinutes` (integer): Execution time (minutes)
  - Range: 1～180
- `requestsPerSecond` (integer): Requests per second
  - Range: 1～1000
- `triggerPercentage` (integer): Delay occurrence probability (%)
  - Range: 0～100
- `delayMilliseconds` (integer): Delay time (milliseconds)
  - Range: 1～10000

**Response:**
- On success: `200 OK` - Scenario status
- Already running: `409 Conflict` - Error message

**Usage Example:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/probabilistic-latency/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":30,"requestsPerSecond":50,"triggerPercentage":20,"delayMilliseconds":1000}'
```

#### Stop

**Endpoint:** `POST /api/ScenarioToggle/probabilistic-latency/stop`

**Parameters:** None

**Response:**
- On success: `200 OK` - Scenario status

**Usage Example:**
```bash
curl -X POST http://localhost:5000/api/ScenarioToggle/probabilistic-latency/stop
```

---

## Common Specifications

### Content-Type

- GET requests: No parameters or URL parameters
- POST requests: `Content-Type: application/json`

### Response Format

All responses are in JSON or plain text format.

### Error Responses

**400 Bad Request:**
```json
"Parameter is out of range"
```

**409 Conflict:**
```json
"Scenario CpuSpike is already running."
```

**500 Internal Server Error:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "traceId": "00-xxxxx-xxxxx-00"
}
```

### CORS

CORS is not enabled by default. Configure in `Startup.cs` if needed.

### Authentication/Authorization

This application has no authentication or authorization mechanisms. Use only in test environments and restrict access at the network level.

---

## Next Steps

- [Architecture Overview](architecture-en.md) - System overview
- [Developer Guide](development-guide-en.md) - How to customize
- [Scenario List](scenarios-en.md) - Detailed scenario descriptions
