English | [日本語](./directtest-api.md)

# DirectTest API - External Load Tool Endpoints

This document describes the DirectTest API in detail, designed for direct calls from external load testing tools like JMeter.

## Overview

DirectTest API is a simple HTTP API designed for easy integration with load testing tools. All endpoints use GET method and control behavior via query parameters.

**Base URL:** `/api/DirectTest`

**Features:**
- Simple GET + query parameters
- Immediate response (after specified processing time)
- Japanese documentation via XML comments
- Full OpenAPI/Swagger support

## OpenAPI/Swagger Documentation

When the application is running, you can access interactive API documentation at:

- **Swagger UI:** `http://localhost:5000/swagger`
- **OpenAPI JSON:** `http://localhost:5000/swagger/v1/swagger.json`

You can test APIs directly from Swagger UI in your browser.

## Endpoint Details

### 1. RandomLatency - Random Response Latency

Simulates response time variation.

**Endpoint:**
```
GET /api/DirectTest/RandomLatency
```

**Parameters:**
| Name | Type | Range | Description |
|------|------|-------|-------------|
| maxLatencyInMilliSeconds | int | 0～30000 | Maximum latency (milliseconds) |

**Response Example:**
```
success:randomlatency (max=1000ms, actual=536ms)
```

**JMeter Configuration Example:**
```
HTTP Request Sampler
  Method: GET
  Path: /api/DirectTest/RandomLatency
  Parameters:
    Name: maxLatencyInMilliSeconds
    Value: ${__Random(100,2000)}  # Random value between 100-2000ms
```

**Use Cases:**
- Observing latency distribution
- Verifying percentile measurements
- Testing timeout settings

---

### 2. RandomException - Random Exceptions

Returns 500 error at specified probability.

**Endpoint:**
```
GET /api/DirectTest/RandomException
```

**Parameters:**
| Name | Type | Range | Description |
|------|------|-------|-------------|
| exceptionPercentage | int | 0～100 | Exception probability (percent) |

**Response Example (Success):**
```
success:randomexception (exceptionPercentage=10%, no exception)
```

**Response Example (Exception):**
```
500 Internal Server Error
Random exception triggered (exceptionPercentage=10%)
```

**JMeter Configuration Example:**
```
HTTP Request Sampler
  Method: GET
  Path: /api/DirectTest/RandomException
  Parameters:
    Name: exceptionPercentage
    Value: 5  # 5% error rate

Response Assertion (Optional)
  Response Code: 200|500  # Allow both codes
```

**Use Cases:**
- Verifying error rate monitoring
- Testing SLO/SLI calculations
- Testing error handling

---

### 3. HighMem - Memory Allocation

Allocates and holds memory of specified size for specified time.

**Endpoint:**
```
GET /api/DirectTest/HighMem
```

**Parameters:**
| Name | Type | Range | Description |
|------|------|-------|-------------|
| secondsToKeepMem | int | 1～300 | Memory hold time (seconds) |
| keepMemSize | int | 1～2048 | Memory size (MB) |

**Response Example:**
```
success:highmem (kept 100MB for 10 seconds)
```

**JMeter Configuration Example:**
```
HTTP Request Sampler
  Method: GET
  Path: /api/DirectTest/HighMem
  Parameters:
    Name: secondsToKeepMem, Value: 10
    Name: keepMemSize, Value: 100
  
  Timeouts:
    Connect: 5000
    Response: 15000  # secondsToKeepMem + buffer
```

**Use Cases:**
- Verifying memory usage monitoring
- Testing memory alerts
- Confirming OOM killer behavior

**Notes:**
- Consider application memory limits
- Recommend setting limits with `--memory` option in Docker environments

---

### 4. HighCPU - High CPU Load

Increases CPU usage with CPU busy loop.

**Endpoint:**
```
GET /api/DirectTest/HighCPU
```

**Parameters:**
| Name | Type | Range | Description |
|------|------|-------|-------------|
| millisecondsToKeepHighCPU | int | 100～60000 | High CPU duration (milliseconds) |

**Response Example:**
```
success:highcpu (ran for 5000ms)
```

**JMeter Configuration Example:**
```
HTTP Request Sampler
  Method: GET
  Path: /api/DirectTest/HighCPU
  Parameters:
    Name: millisecondsToKeepHighCPU
    Value: 5000  # 5 seconds
  
  Timeouts:
    Response: 10000  # millisecondsToKeepHighCPU + buffer
```

**Use Cases:**
- Verifying CPU usage monitoring
- Testing CPU alerts
- Confirming throttling behavior

---

## Practical Load Test Scenarios

### Scenario 1: Normal Traffic + Fault Injection

Simulate realistic traffic:
- 95%: Normal requests (low latency)
- 5%: Errors or high latency

**JMeter Thread Group:**
```
Thread Group "Normal Traffic"
  Threads: 80
  Ramp-up: 10
  Loop: Infinite
  
  HTTP Request "Normal"
    Path: /api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=500
    Weight: 95%
  
  HTTP Request "Error"
    Path: /api/DirectTest/RandomException?exceptionPercentage=100
    Weight: 5%
```

### Scenario 2: Resource Load Test

Combined load of CPU, memory, and latency:

**JMeter Thread Group:**
```
Thread Group "Resource Load"
  Threads: 50
  Ramp-up: 5
  Duration: 300 seconds
  
  HTTP Request "CPU Load"
    Path: /api/DirectTest/HighCPU?millisecondsToKeepHighCPU=3000
    
  HTTP Request "Memory Load"
    Path: /api/DirectTest/HighMem?secondsToKeepMem=5&keepMemSize=50
    
  HTTP Request "Latency"
    Path: /api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=2000
```

### Scenario 3: Gradual Load Increase

Gradually increase load to find system limits:

**JMeter Stepping Thread Group:**
```
Stepping Thread Group
  This group will start: 10 threads
  Next, add: 10 threads
  Every: 30 seconds
  Until reaching: 200 threads
  
  HTTP Request "Gradual Load"
    Path: /api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000
```

---

## Apache Bench Usage Examples

### Basic Load Test

```bash
# 1000 requests, 100 parallel
ab -n 1000 -c 100 \
  "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=500"
```

### Test with Error Rate

```bash
# Test with 5% error rate
ab -n 1000 -c 50 \
  "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=5"
```

### With CSV Output

```bash
ab -n 1000 -c 100 -g results.tsv \
  "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000"

# Graph with gnuplot
gnuplot << EOF
set terminal png
set output "latency.png"
set datafile separator "\t"
plot "results.tsv" using 9 with lines title "Response Time"
EOF
```

---

## Simple Testing with curl

### Single Tests

```bash
# Latency test
curl "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000"

# Exception test (success pattern)
curl "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=0"

# Exception test (failure pattern)
curl "http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=100"
```

### Parallel Execution

```bash
# Execute 10 requests in parallel
for i in {1..10}; do
  curl -s "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=2000" &
done
wait
echo "All requests completed"
```

### Continuous Load Generation

```bash
# 60 seconds, 5 requests per second
for i in {1..60}; do
  for j in {1..5}; do
    curl -s "http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=500" &
  done
  sleep 1
done
```

---

## Integration with Monitoring

### Prometheus Metrics Monitoring

Recommended metrics to monitor during DirectTest API load testing:

```promql
# Request count
rate(http_requests_total{endpoint=~"/api/DirectTest.*"}[1m])

# Latency (P50, P95, P99)
histogram_quantile(0.50, http_request_duration_seconds_bucket{endpoint=~"/api/DirectTest.*"})
histogram_quantile(0.95, http_request_duration_seconds_bucket{endpoint=~"/api/DirectTest.*"})
histogram_quantile(0.99, http_request_duration_seconds_bucket{endpoint=~"/api/DirectTest.*"})

# Error rate
rate(http_requests_total{endpoint=~"/api/DirectTest.*",status_code="500"}[1m])
/ rate(http_requests_total{endpoint=~"/api/DirectTest.*"}[1m])
```

### Application Insights Monitoring

For Azure Application Insights:

```kusto
// Request statistics
requests
| where url contains "DirectTest"
| summarize 
    count=count(),
    avg_duration=avg(duration),
    p95_duration=percentile(duration, 95),
    success_rate=100.0 * countif(success == true) / count()
  by bin(timestamp, 1m)
```

---

## Troubleshooting

### Timeout Errors

**Cause:** Timeout set shorter than specified parameter time

**Solution:**
```
# For JMeter
HTTP Request Sampler > Timeouts
  Connect Timeout: 5000
  Response Timeout: (specified time × 1.5) or more

# For curl
curl --max-time 30 "http://localhost:5000/api/DirectTest/HighCPU?millisecondsToKeepHighCPU=20000"
```

### Out of Memory Errors

**Cause:** keepMemSize too large or too many parallel requests

**Solution:**
- Reduce keepMemSize
- Reduce parallel count
- Increase Docker memory limit: `docker run --memory="4g" ...`

### 400 Bad Request Errors

**Cause:** Parameter out of range

**Solution:**
```bash
# Error example
curl "http://localhost:5000/api/DirectTest/HighCPU?millisecondsToKeepHighCPU=100000"
# → 400: millisecondsToKeepHighCPU must be between 100 and 60000.

# Correct example
curl "http://localhost:5000/api/DirectTest/HighCPU?millisecondsToKeepHighCPU=60000"
```

---

## Best Practices

### 1. Gradually Increase Load

```
Start: 10 threads
  ↓ After 1 minute
Mid: 50 threads
  ↓ After 1 minute
Max: 100 threads
```

### 2. Set Timeout with Margin

```
Response Timeout = Parameter Value × 1.5 + Connection Time
```

### 3. Set Resource Limits

```bash
# For Docker
docker run \
  --memory="2g" \
  --cpus="2" \
  -p 5000:8080 \
  sre-agent-tester
```

### 4. Monitor Metrics While Running

- CPU usage
- Memory usage
- Response time
- Error rate

### 5. Provide Cooldown Period

Set 30-60 second wait time between scenarios to stabilize system

---

## Next Steps

- [API Reference](api-reference-en.md) - All API endpoint details
- [Scenario List](scenarios-en.md) - Other test scenarios
- [Architecture](architecture-en.md) - System overview
- [Developer Guide](development-guide-en.md) - Customization methods

Start running load tests to verify your monitoring systems and SRE practices!
