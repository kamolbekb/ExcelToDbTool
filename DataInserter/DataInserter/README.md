# DataInserter - Excel to PostgreSQL User Import Tool

A high-performance, cross-platform .NET 8 application for importing user data from Excel files into PostgreSQL databases.

## Features

- 🚀 Batch processing for large Excel files
- 🔄 Automatic retry on transient failures
- 📊 Progress tracking and performance metrics
- 🛡️ Duplicate detection and handling
- 📝 Structured logging with Serilog
- 🌍 Cross-platform support (Windows, Linux, macOS)
- ⚡ Connection pooling for better performance
- 🎯 Clean architecture with dependency injection

## Prerequisites

- .NET 8 SDK
- PostgreSQL database (two databases: IAM and SDG)
- Excel file with user data

## Quick Start

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd DataInserter
   ```

2. **Configure the application**
   - Copy `appsettings.example.json` to `appsettings.json`
   - Update the connection strings for your PostgreSQL databases
   - Set the path to your Excel file

3. **Run the application**
   ```bash
   dotnet run
   ```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "IAMConnection": "Server=localhost;Database=LocalIAMDB;User Id=postgres;Password=yourpassword;",
    "SDGConnection": "Server=localhost;Database=LocalSDGDB;User Id=postgres;Password=yourpassword;"
  },
  "ApplicationSettings": {
    "ExcelPath": "./data/users.xlsx",
    "BatchSize": 100,
    "MaxRetryAttempts": 3,
    "RetryDelayMilliseconds": 1000
  }
}
```

### Excel File Format

The Excel file should have the following columns (starting from row 3):
- Column 1: Row Number
- Column 2: Name
- Column 3: Email
- Column 5: Role
- Column 6: User Group
- Column 7: Section (optional)
- Column 8: Division

## Command Line Options

Override configuration via command line:

```bash
# Override Excel path
dotnet run --ApplicationSettings:ExcelPath="/path/to/file.xlsx"

# Override batch size
dotnet run --ApplicationSettings:BatchSize=50

# Override connection string
dotnet run --ConnectionStrings:IAMConnection="Server=prod-server;..."
```

## Cross-Platform Usage

### Windows
```bash
dotnet run --ApplicationSettings:ExcelPath="C:/Users/username/data.xlsx"
```

### Linux/macOS
```bash
dotnet run --ApplicationSettings:ExcelPath="/home/username/data.xlsx"
```

## Output

- **Logs**: `./Logs/DataInserterLog_[timestamp].txt`
- **Duplicates**: `./DuplicateRecords/duplicates_[timestamp].txt`

## Building and Publishing

```bash
# Build for current platform
dotnet build -c Release

# Publish for Windows
dotnet publish -c Release -r win-x64

# Publish for Linux
dotnet publish -c Release -r linux-x64

# Publish for macOS
dotnet publish -c Release -r osx-x64
```

## Architecture

- **Services**: Business logic layer
  - `ExcelReaderService`: Reads and parses Excel files
  - `UserProcessingService`: Processes users with retry logic
  - `DuplicateHandlerService`: Handles duplicate detection

- **Repositories**: Data access layer
  - `IamRepository`: IAM database operations
  - `SdgRepository`: SDG database operations

- **Utilities**: Helper functions
  - `StringNormalizer`: String manipulation utilities
  - `RoleMapper`: Role and user group mapping logic

## Performance

- Processes ~1000 users per minute (depending on database performance)
- Memory efficient batch processing
- Connection pooling for optimal database usage
- Retry logic for transient failures

## Troubleshooting

1. **"No service for type 'Serilog.ILogger' has been registered"**
   - Ensure all NuGet packages are restored: `dotnet restore`

2. **Excel file not found**
   - Check the file path in appsettings.json
   - Use absolute paths or ensure relative paths are correct

3. **Database connection errors**
   - Verify PostgreSQL is running
   - Check connection strings
   - Ensure databases (LocalIAMDB and LocalSDGDB) exist

## License

[Your License Here]

## Contributing

[Your Contributing Guidelines Here]
