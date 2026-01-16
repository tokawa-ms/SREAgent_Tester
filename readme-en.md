English | [日本語](./readme.md)

# SRE Agent Tester

A diagnostic scenario application built with .NET 8 / ASP.NET Core. You can safely verify the SRE Agent monitoring pipeline by controlling immediate execution scenarios from `/Home/Index` and continuous test scenarios running in the background from `/Home/ToggleScenarios`.
You can also check API documentation from `/swagger` and perform tests such as directly loading the API.

## Setup

1. Clone the repository and navigate to the project root (`SREAgent_Tester`).
2. Restore dependencies and build.
   ```bash
   dotnet restore
   dotnet build
   ```
3. Start the application.
   ```bash
   dotnet run --project DiagnosticScenarios/DiagnosticScenarios.csproj
   ```
4. Open `http://localhost:5000/` in your browser to access the top page.

Hosting on Docker / Azure App Service is also available as usual.

### Running in Docker Container

1. Build the Docker image at the root.
   ```bash
   docker build -t sre-agent-tester .
   ```
2. Start with ports exposed to the host (e.g., 8080).
   ```bash
   docker run --rm -p 8080:8080 -e ASPNETCORE_URLS=http://+:8080 sre-agent-tester
   ```
   - Since HTTPS is not required, only HTTP ports are exposed with `ASPNETCORE_URLS`.
   - To use the default `appsettings.Development.json`, add `--env ASPNETCORE_ENVIRONMENT=Development`.
3. If you have multiple containers or dependent services, you can use the included Compose definition with `docker-compose up -d`.

To stop, press `Ctrl+C` to exit or run `docker stop <CONTAINER_ID>` from the terminal.

## UI and Scenarios

- `Home/Index`: Demo cards that call APIs for immediate firing such as exception bursts, memory spikes, and high CPU load.
- `Home/ToggleScenarios`: Toggle to start ProbabilisticFailure / CpuSpike / MemoryLeak / ProbabilisticLatency and check scheduled times and execution status.
- `/api/DirectTest`: Simple API endpoints for external load tools like JMeter. See `docs/directtest-api-en.md` for details.

API parameters and operational notes for each scenario are listed in `docs/scenarios-en.md`. Be sure to check before operation.

## API Documentation

When the application is running, you can access OpenAPI/Swagger documentation at the following URLs:
- Swagger UI: `http://localhost:5000/swagger`
- OpenAPI JSON: `http://localhost:5000/swagger/v1/swagger.json`

## Main Code

- `DiagnosticScenarios/Controllers/DiagnosticScenarios.cs`
  - DiagScenarioController - Immediate execution scenario API. Memory leak / spike processing has been refactored to be thread-safe.
- `DiagnosticScenarios/Controllers/DirectTestController.cs`
  - DirectTestController - Simple API endpoints for external load tools (RandomLatency, RandomException, HighMem, HighCPU).
- `DiagnosticScenarios/Controllers/ScenarioToggleController.cs`
  - Start / stop endpoints for background execution scenarios.
- `DiagnosticScenarios/Services/ScenarioToggleService.cs`
  - Implements worker loops and state management for toggle scenarios.

## Notes

- All are for test / verification environments only. **Never run in production environment.**
- Parameters should be adjusted according to the CPU / memory of your environment, as they may receive more load than expected.
- During execution, it is effective to collect runtime metrics with `dotnet-counters`, `dotnet-trace`, etc.

## Reference

This application is customized based on [Diagnostic scenarios sample debug target](https://github.com/dotnet/samples/tree/main/core/diagnostics/DiagnosticScenarios).

## License

See [LICENSE](LICENSE) and [LICENSE-MIT](LICENSE-MIT) for license information on this repository.
Code derived from dotnet/samples is CC BY 4.0, and other original code is MIT License.
Source, license URL, and changes are described in [NOTICE](NOTICE).
