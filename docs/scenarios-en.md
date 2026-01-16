English | [日本語](./scenarios.md)

# Scenario List

This document describes all scenarios available in SREAgent_Tester in detail.

## Table of Contents

- [Immediate Execution Scenarios](#immediate-execution-scenarios)
- [External Load Tool API](#external-load-tool-api)
- [Toggle Scenarios](#toggle-scenarios)
- [Execution Notes](#execution-notes)
- [Recommended Scenarios by Use Case](#recommended-scenarios-by-use-case)

---

## Immediate Execution Scenarios

Base URL: `/api/DiagScenario`

These scenarios execute immediately upon receiving HTTP request and do not return response until completion. Ideal for one-shot tests or reproducing specific issues.

### Scenario List Table

| Endpoint | Description | Main Parameters | Estimated Duration |
| --- | --- | --- | --- |
| deadlock | Generate many threads that mutually lock each other to reproduce hang | None | Persistent (hang) |
| highcpu/{milliseconds} | Occupy CPU with busy loop for specified time | milliseconds (100-30000) | As per parameter |
| memspike/{seconds} | Repeatedly allocate large memory at 5-second intervals and release via GC | seconds (1-1800) | As per parameter |
| memleak/{kilobytes} | Retain object of specified size to intentionally leak memory | kilobytes (1-10240) | Instant |
| exception | Throw exception immediately | None | Instant |
| exceptionburst/{durationSeconds}/{exceptionsPerSecond} | Continuously throw exceptions at high frequency to reproduce log/metric saturation | durationSeconds (1-1800), exceptionsPerSecond (1-1000) | As per parameter |
| probabilisticload/{durationSeconds}/{requestsPerSecond}/{exceptionPercentage} | Make pseudo backend calls and generate exceptions probabilistically | durationSeconds, requestsPerSecond, exceptionPercentage | As per parameter |
| taskwait | Reproduce thread pool exhaustion with deprecated synchronous wait pattern | None | ~0.5 seconds |
| tasksleepwait | Reproduce thread pool exhaustion with deprecated spin wait pattern | None | ~0.5 seconds |
| taskasyncwait | Sample for comparison with correct await pattern | None | ~0.5 seconds |

For detailed usage and samples, see [API Reference](api-reference-en.md).

---

## External Load Tool API

Base URL: `/api/DirectTest`

These endpoints are designed to be called directly from external load testing tools like JMeter or Apache Bench. Can generate various load patterns with simple query parameters.

### Endpoint List Table

| Endpoint | Description | Main Parameters | Estimated Duration |
| --- | --- | --- | --- |
| RandomLatency | Generate random response delay in range from 0 to specified value | maxLatencyInMilliSeconds (0-30000) | As per parameter |
| RandomException | Generate exception at specified probability and return 500 error | exceptionPercentage (0-100) | Instant |
| HighMem | Allocate and hold memory of specified size for specified time | secondsToKeepMem (1-300), keepMemSize (1-2048 MB) | As per parameter |
| HighCPU | Execute CPU busy loop for specified time | millisecondsToKeepHighCPU (100-60000) | As per parameter |

**Usage Examples (JMeter):**
```
# Random latency
GET http://localhost:5000/api/DirectTest/RandomLatency?maxLatencyInMilliSeconds=1000

# 10% error probability
GET http://localhost:5000/api/DirectTest/RandomException?exceptionPercentage=10

# Hold 100MB for 10 seconds
GET http://localhost:5000/api/DirectTest/HighMem?secondsToKeepMem=10&keepMemSize=100

# High CPU for 5 seconds
GET http://localhost:5000/api/DirectTest/HighCPU?millisecondsToKeepHighCPU=5000
```

For detailed specifications, see [API Reference](api-reference-en.md#external-load-tool-api).

---

## Toggle Scenarios

Base URL: `/api/ScenarioToggle`

These scenarios run long-term as background tasks and can be controlled individually for start/stop. Ideal for continuous load testing and long-term fault injection.

### Scenario List Table

| Scenario | Start / Stop | Main Configuration Fields | Use Case |
| --- | --- | --- | --- |
| ProbabilisticFailure | /probabilistic-failure/start / stop | durationMinutes, requestsPerSecond, failurePercentage | Probabilistic fault injection |
| CpuSpike | /cpu-spike/start / stop | durationMinutes, intervalSeconds, triggerPercentage, spikeSeconds | Irregular CPU spikes |
| MemoryLeak | /memory-leak/start / stop | durationMinutes, intervalSeconds, triggerPercentage, memoryMegabytes, holdSeconds | Gradually progressing memory leak |
| ProbabilisticLatency | /probabilistic-latency/start / stop | durationMinutes, requestsPerSecond, triggerPercentage, delayMilliseconds | Probabilistic latency injection |

### UI (Web Interface)

At `/Home/ToggleScenarios`, you can control each toggle scenario via GUI.

**Features:**
- Input configuration fields for each scenario
- Toggle on/off with switch
- Status auto-updates every 5 seconds
- Display scheduled end time and last message

**How to Use:**
1. Open http://localhost:5000/Home/ToggleScenarios in browser
2. Input scenario parameters
3. Turn toggle switch ON
4. Verify status becomes "Running"
5. Turn toggle switch OFF to stop if needed

---

## Execution Notes

### Environment Notes

⚠️ **Important:**
- **Use at your own risk**
- **Execute only in test/verification environments**
- **Never execute in production environment**

### Resource Management

1. **Gradually Increase Load**
   - Excessive values will exhaust host OS or container resources
   - Gradually increase while monitoring metrics

2. **Monitor Metrics**
   - CPU usage
   - Memory usage
   - Disk I/O
   - Network bandwidth

3. **Set Resource Limits**
   ```bash
   # For Docker
   docker run --memory="2g" --cpus="2" sre-agent-tester
   ```

### Cooldown Time

After stopping scenarios, it may take several seconds to tens of seconds for garbage collection to complete. Provide sufficient cooldown time.

- **CPU Spike**: 1-5 seconds
- **Memory Spike**: 5-30 seconds (until GC completes)
- **Memory Leak**: 10-60 seconds (multiple GC cycles)

---

## Recommended Scenarios by Use Case

### Case 1: APM Tool Verification

**Goal:** Verify application monitoring tools work correctly

**Recommended Scenarios:**
```bash
# CPU monitoring
curl http://localhost:5000/api/DiagScenario/highcpu/10000

# Memory monitoring
curl http://localhost:5000/api/DiagScenario/memspike/120

# Error rate monitoring (toggle)
curl -X POST http://localhost:5000/api/ScenarioToggle/probabilistic-failure/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":15,"requestsPerSecond":50,"failurePercentage":10}'
```

### Case 2: SLO/SLI Calculation Verification

**Goal:** Verify SLO dashboard and error budget calculations are accurate

**Recommended Scenarios:**
```bash
# Test with known error rate (Expected: 5% error rate, 95% SLI)
curl "http://localhost:5000/api/DiagScenario/probabilisticload/300/100/5"
```

### Case 3: Alert Rule Testing

**Goal:** Verify threshold-based alerts fire and resolve correctly

**Recommended Scenarios:**
```bash
# CPU alert test (alerts should fire→resolve repeatedly)
curl -X POST http://localhost:5000/api/ScenarioToggle/cpu-spike/start \
  -H "Content-Type: application/json" \
  -d '{"durationMinutes":30,"intervalSeconds":60,"triggerPercentage":80,"spikeSeconds":15}'
```

### Case 4: Debug Tool Training

**Goal:** Developers learn to use debug tools

**Recommended Scenarios:**
```bash
# Deadlock detection practice (get thread dump with dotnet-dump or jstack)
curl http://localhost:5000/api/DiagScenario/deadlock

# Memory leak detection practice (get heap dump with dotnet-gcdump)
curl http://localhost:5000/api/DiagScenario/memleak/5120
curl http://localhost:5000/api/DiagScenario/memleak/5120
```

---

## Next Steps

- API Details: [api-reference-en.md](api-reference-en.md)
- Architecture Understanding: [architecture-en.md](architecture-en.md)
- Customization Methods: [development-guide-en.md](development-guide-en.md)

Start executing scenarios to verify your monitoring tools and SRE practices!
