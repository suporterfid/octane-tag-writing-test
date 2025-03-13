# Octane Tag Writing Test

A comprehensive .NET 8 testing suite for evaluating RFID tag writing operations using the Impinj Octane SDK. This project implements various test strategies to assess different aspects of RFID tag writing performance, reliability, and functionality.

## Overview

This application provides a structured testing framework for RFID tag writing operations, implementing various test strategies to evaluate different aspects of tag writing performance and reliability. The project uses the Strategy design pattern to organize different test scenarios, making it easy to add new test cases while maintaining a consistent interface.

## Prerequisites

- .NET 8.0 SDK
- Impinj Octane SDK (v5.0.0)
- LLRP SDK (included with Octane SDK)
- Impinj RFID Reader (hostname/IP address required)
- Docker (optional, for containerized execution)

## Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
  </PropertyGroup>
</Project>
```

### NuGet Packages
- OctaneSDK (v5.0.0)
- Microsoft.VisualStudio.Azure.Containers.Tools.Targets (v1.21.0)

## Project Structure

```
OctaneTagWritingTest/
├── Helpers/
│   ├── EpcListManager.cs     # Manages EPC list operations
│   └── TagOpController.Instance.cs    # Controls tag operations
├── TestStrategy/
│   ├── TestCase1SpeedStrategy.cs           # Optimal write speed testing
│   ├── TestCase2InlineWriteStrategy.cs     # Inline writing operations
│   ├── TestCase3MultiAntennaWriteStrategy.cs # Multi-antenna writing
│   ├── TestCase4BatchSerializationTestStrategy.cs # Batch operations
│   ├── TestCase5VerificationCycleStrategy.cs # Write verification
│   ├── TestCase6RobustnessStrategy.cs      # Robustness testing
│   ├── TestCase7ErrorRecoveryStrategy.cs   # Error recovery testing
│   └── TestCase8EnduranceStrategy.cs       # Endurance testing
├── BaseTestStrategy.cs       # Base class for all test strategies
├── ITestStrategy.cs         # Strategy pattern interface
├── TestManager.cs          # Manages test execution
└── Program.cs             # Application entry point
```

## Test Strategies

1. **Speed Test (TestCase1)**
   - Optimizes for maximum write speed
   - Measures and logs write operation timing
   - Records results in TestCase1_Log.csv

2. **Inline Write Test (TestCase2)**
   - Tests inline writing capabilities
   - Evaluates continuous write operations

3. **Multi-Antenna Write Test (TestCase3)**
   - Tests writing across multiple antennas
   - Evaluates antenna switching and coordination

4. **Batch Serialization Test (TestCase4)**
   - Tests batch writing operations
   - Evaluates serialization performance

5. **Verification Cycle Test (TestCase5)**
   - Implements write-verify cycles
   - Ensures data integrity

6. **Robustness Test (TestCase6)**
   - Tests system stability
   - Evaluates error handling

7. **Error Recovery Test (TestCase7)**
   - Tests recovery from various error conditions
   - Evaluates system resilience

8. **Endurance Test (TestCase8)**
   - Long-running stability testing
   - Evaluates system performance over time

## Features

- Strategy Pattern implementation for flexible test case management
- Comprehensive logging of test results
- Support for various RFID operations:
  - EPC writing
  - Access password updates
  - Block writing operations
  - Multi-antenna operations
- Low latency reporting configuration
- CSV-based result logging
- Docker support for containerized execution

## Usage

1. Run the application with the reader's hostname as an argument:
```bash
dotnet run <reader-hostname>
```

2. Select a test strategy from the menu by entering the corresponding number.

3. Test results will be logged to strategy-specific CSV files (e.g., TestCase1_Log.csv).

## Docker Support

The project includes multi-stage Docker support for both development and production environments:

### Docker Configuration
- Base image: `mcr.microsoft.com/dotnet/runtime:8.0`
- SDK image: `mcr.microsoft.com/dotnet/sdk:8.0`
- Multi-stage build process for optimized container size
- Supports both Debug and Release configurations

### Build and Run
```bash
# Build the Docker image
docker build -t octane-tag-writing-test .

# Run in production mode
docker run octane-tag-writing-test <reader-hostname>

# Build with specific configuration
docker build --build-arg BUILD_CONFIGURATION=Debug -t octane-tag-writing-test .
```

### Docker Stages
1. **Base**: Runtime environment setup
2. **Build**: Compilation of the project
3. **Publish**: Creation of deployment artifacts
4. **Final**: Production runtime environment

The Dockerfile is optimized for both Visual Studio debugging and production deployment.

### Docker Build Optimization
The project includes a `.dockerignore` file that excludes unnecessary files from the Docker context:
- Development artifacts (bin/, obj/)
- Version control files (.git/, .gitignore)
- IDE files (.vs/, .vscode/, *.user files)
- Configuration files (secrets.dev.yaml, values.dev.yaml)
- Node.js files (if any)
- Docker-related files (docker-compose*, Dockerfile*)

This optimization ensures:
- Smaller build context
- Faster builds
- Better security (by excluding sensitive files)
- Cleaner production images

## Logging

Each test strategy generates a CSV log file with relevant metrics:
- Timestamp
- TID (Tag ID)
- Old EPC
- New EPC
- Write Time
- Operation Result

## Base Configuration

The base test strategy provides common functionality:
- Reader connection management
- Default settings configuration
- Low latency reporting
- EPC list management
- CSV logging

## Contributing

To add a new test strategy:
1. Create a new class in the TestStrategy folder
2. Inherit from BaseTestStrategy
3. Implement the RunTest() method
4. Register the strategy in TestManager.cs

## License

[Your License Here]

## Notes

- Ensure proper reader connectivity before running tests
- Review individual test strategy documentation for specific requirements
- Check CSV log files for detailed test results
