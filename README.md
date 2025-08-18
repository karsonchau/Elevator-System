# Elevator System

A production-ready elevator control system with robust processing, health monitoring, and event-driven architecture built with .NET 8.

## Architecture

This project follows Clean Architecture principles with clear separation of concerns:

- **Domain Layer** (`ElevatorSystem.Domain`) - Core business entities and enums
- **Application Layer** (`ElevatorSystem.Application`) - Business logic, services, and interfaces  
- **Infrastructure Layer** (`ElevatorSystem.Infrastructure`) - Data persistence and external dependencies
- **Presentation Layer** (`ElevatorSystem.Presentation`) - Console application entry point

## Key Features

- **Production-Ready Architecture** - Robust processing with retry policies, health monitoring, and circuit breakers
- **Event-Driven Design** - Comprehensive event infrastructure for monitoring and integration
- **Command-Based Processing** - CQRS pattern with reliable request processing pipeline  
- **Health Monitoring** - Real-time system health tracking and performance metrics
- **Retry & Circuit Breaker** - Exponential backoff and failure protection mechanisms
- **File-Based Scenarios** - Load elevator request scenarios from JSON configuration
- **Comprehensive Logging** - Detailed logging of elevator movements and request processing
- **Queue-Ready Architecture** - Designed for easy integration with external message queues

## System Assumptions

- **Single Elevator** - The system is designed to manage one elevator
- **Closed System** - No external database; uses in-memory storage for queues and state
- **Sequential Processing** - Requests are processed in optimal order based on elevator position and direction
- **Time-Based Simulation** - Scenarios include timing delays to simulate real-world request patterns

## Getting Started

### Prerequisites

- .NET 8 SDK
- Any IDE supporting .NET (Visual Studio, VS Code, Rider)

### Running the Simulator

1. **Clone and navigate to the project:**
   ```bash
   cd ElevatorSystem
   ```

2. **Build the solution:**
   ```bash
   dotnet build
   ```

3. **Run the simulator:**
   ```bash
   cd ElevatorSystem.Presentation
   dotnet run
   ```

The simulator will:
- Initialize the elevator system with settings from `appsettings.json`
- Load scenarios from `scenarios.json` (located in project root)
- Execute the simulation with time-based request processing
- Display real-time logging of elevator operations

### Configuration

#### Configuration (`appsettings.json`)

```json
{
  "ElevatorSettings": {
    "MinFloor": 1,
    "MaxFloor": 10,
    "NumberOfElevators": 1,
    "FloorMovementTimeMs": 1000,
    "LoadingTimeMs": 1000
  },
  "SimulationSettings": {
    "ScenarioFilePath": "scenarios.json"
  },
  "RequestProcessing": {
    "RequestTimeoutMs": 30000,
    "MaxRetryAttempts": 3,
    "CircuitBreakerFailureThreshold": 5,
    "HealthCheckIntervalMs": 5000
  }
}
```

- `MinFloor/MaxFloor` - Building floor range
- `FloorMovementTimeMs` - Time to move between floors
- `LoadingTimeMs` - Time for passenger loading/unloading
- `ScenarioFilePath` - Path to scenarios file (relative to project root)
- `RequestTimeoutMs` - Maximum time before request is considered timed out
- `MaxRetryAttempts` - Number of retry attempts for failed requests
- `CircuitBreakerFailureThreshold` - Failures needed to trigger circuit breaker
- `HealthCheckIntervalMs` - Interval for system health monitoring

## Updating Scenarios

### Scenario Format

Edit the `scenarios.json` file in the project root to customize elevator requests:

```json
[
  {
    "currentFloor": 1,
    "destinationFloor": 5,
    "requestTimeMs": 0
  },
  {
    "currentFloor": 3,
    "destinationFloor": 8,
    "requestTimeMs": 1000
  }
]
```

### Scenario Properties

- `currentFloor` - Floor where passenger is waiting
- `destinationFloor` - Floor where passenger wants to go
- `requestTimeMs` - Delay in milliseconds before this request is made (from simulation start)

### Example Scenarios

**Rush Hour Pattern:**
```json
[
  {"currentFloor": 1, "destinationFloor": 8, "requestTimeMs": 0},
  {"currentFloor": 1, "destinationFloor": 5, "requestTimeMs": 500},
  {"currentFloor": 1, "destinationFloor": 10, "requestTimeMs": 1000},
  {"currentFloor": 1, "destinationFloor": 3, "requestTimeMs": 1500}
]
```

**Mixed Usage Pattern:**
```json
[
  {"currentFloor": 1, "destinationFloor": 5, "requestTimeMs": 0},
  {"currentFloor": 8, "destinationFloor": 1, "requestTimeMs": 2000},
  {"currentFloor": 3, "destinationFloor": 7, "requestTimeMs": 4000},
  {"currentFloor": 9, "destinationFloor": 2, "requestTimeMs": 6000}
]
```

## Running Tests

Execute the test suite to verify system functionality:

```bash
dotnet test
```

Tests cover:
- Domain entity behavior
- Application service logic
- Repository implementations
- Integration scenarios

## Project Structure

```
ElevatorSystem/
├── ElevatorSystem.Domain/          # Core entities and enums
│   ├── Entities/
│   └── Enums/
├── ElevatorSystem.Application/     # Business logic and services
│   ├── Configuration/
│   ├── Interfaces/
│   ├── Models/
│   └── Services/
├── ElevatorSystem.Infrastructure/  # Data persistence and DI
│   └── Repositories/
├── ElevatorSystem.Presentation/    # Console application
├── ElevatorSystem.Tests/           # Unit and integration tests
└── scenarios.json                  # Simulation scenarios
```

## Key Components

### Core Services
- **ElevatorController** - Production-ready controller with failure handling and monitoring
- **ElevatorMovementService** - Handles elevator movement logic and optimization
- **ElevatorRequestManager** - Manages pickup and dropoff operations
- **ScenarioFileReader** - Loads simulation scenarios from JSON files

### Robust Processing Pipeline
- **RetryPolicyManager** - Exponential backoff and circuit breaker implementation
- **HealthMonitor** - System health tracking and performance metrics
- **RequestStatusTracker** - Request timeout monitoring and status management

### Event & Command Infrastructure  
- **Command Handlers** - Reliable request processing with validation and retry logic
- **Event System** - Comprehensive event logging and system monitoring
- **Message Attributes** - Queue-ready metadata for external message systems

## Logging & Monitoring

The system provides comprehensive logging and monitoring including:
- **Request Processing** - Full request lifecycle tracking with retry attempts
- **System Health** - Performance metrics, success rates, and health status
- **Circuit Breaker** - Failure tracking and circuit breaker status changes
- **Elevator Operations** - Floor-by-floor movement and passenger operations
- **Event System** - Complete audit trail of all elevator events

Monitor the console output during simulation to observe system behavior, health metrics, and performance characteristics.

## Production Considerations

### Scalability
- **Current**: Single-instance deployment with in-memory storage
- **Production**: Replace InMemoryEventBus/CommandBus with distributed alternatives:
  - Azure Service Bus, RabbitMQ, or Kafka for event/command processing
  - SQL Server, PostgreSQL, or MongoDB for persistent storage
  - Redis for caching and distributed state management

### High Availability
- **Health Monitoring**: Built-in health checks ready for load balancer integration
- **Circuit Breaker**: Automatic failure protection and recovery
- **Retry Logic**: Exponential backoff for transient failure handling
- **Queue Ready**: Message attributes prepared for external queue systems

### Deployment
The system is designed for containerized deployment with:
- Configurable settings via `appsettings.json` or environment variables
- Health check endpoints for Kubernetes/Docker health monitoring
- Structured logging ready for centralized log aggregation
- Metrics collection for monitoring dashboards