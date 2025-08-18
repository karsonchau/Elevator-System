# Elevator System

A clean architecture implementation of an elevator control system simulator built with .NET 8.

## Architecture

This project follows Clean Architecture principles with clear separation of concerns:

- **Domain Layer** (`ElevatorSystem.Domain`) - Core business entities and enums
- **Application Layer** (`ElevatorSystem.Application`) - Business logic, services, and interfaces  
- **Infrastructure Layer** (`ElevatorSystem.Infrastructure`) - Data persistence and external dependencies
- **Presentation Layer** (`ElevatorSystem.Presentation`) - Console application entry point

## Key Features

- **Single Elevator System** - Currently designed for one elevator operations
- **In-Memory Storage** - Uses in-memory repositories for elevator and request queues (closed system)
- **File-Based Scenarios** - Load elevator request scenarios from JSON configuration
- **Real-Time Simulation** - Time-based request processing with configurable timing
- **Comprehensive Logging** - Detailed logging of elevator movements and request processing

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

#### Elevator Settings (`appsettings.json`)

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
  }
}
```

- `MinFloor/MaxFloor` - Building floor range
- `FloorMovementTimeMs` - Time to move between floors
- `LoadingTimeMs` - Time for passenger loading/unloading
- `ScenarioFilePath` - Path to scenarios file (relative to project root)

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

- **ElevatorController** - Manages elevator request assignment and processing
- **ElevatorMovementService** - Handles elevator movement logic and timing
- **ElevatorRequestManager** - Manages pickup and dropoff operations
- **ScenarioFileReader** - Loads simulation scenarios from JSON files
- **InMemoryRepositories** - Provide data persistence for the closed system

## Logging

The system provides comprehensive logging including:
- Elevator initialization and configuration
- Request processing and assignment
- Floor-by-floor movement tracking
- Passenger pickup and dropoff events
- System state verification

Monitor the console output during simulation to observe elevator behavior and system performance.