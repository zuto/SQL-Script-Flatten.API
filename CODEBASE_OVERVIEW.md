# SQL-Script-Flatten.API - Codebase Overview

**Last Updated:** 21/11/2025  
**Framework:** .NET 8.0  
**Purpose:** SQL script testing utility for non-destructive database change validation

---

## Table of Contents
1. [Project Purpose](#project-purpose)
2. [Architecture Overview](#architecture-overview)
3. [Core Functionality](#core-functionality)
4. [Technology Stack](#technology-stack)
5. [Project Structure](#project-structure)
6. [Key Components](#key-components)
7. [API Endpoints](#api-endpoints)
8. [Database Configuration](#database-configuration)
9. [Script Processing Flow](#script-processing-flow)
10. [Important Notes](#important-notes)

---

## Project Purpose

This API provides a safe way to test SQL scripts against a production-like database without making permanent changes. It:

- **Wraps SQL scripts** in transaction logic
- **Creates snapshots** of affected tables before execution
- **Executes the script** within a transaction
- **Shows what changed** by comparing before/after states
- **Rolls back everything** - no permanent database modifications

**Use Case:** Developers can test UPDATE/INSERT/DELETE scripts to see exactly what rows would be affected before running them in production.

---

## Architecture Overview

### Layer Structure

```
┌─────────────────────────────────────┐
│         API Layer                   │
│  (Controllers + Services)           │
│  - HTTP endpoints                   │
│  - Business logic                   │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│         Data Layer                  │
│  (Repositories + Dapper)            │
│  - Database access                  │
│  - Connection management            │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│      SQL Server Database            │
│      (HorizonQA)                    │
└─────────────────────────────────────┘
```

### Unused Layers
- **BL Layer** (`src/BL/`) - Empty, can be ignored
- **Domain Layer** (`src/Domain/`) - Empty, can be ignored

---

## Core Functionality

### What the API Does

When you POST a SQL script to `/script`, the API:

1. **Parses** the input SQL to identify referenced tables
2. **Validates** table names against a whitelist (~600 tables)
3. **Generates** a modified script that:
   ```sql
   BEGIN TRANSACTION;
   
   -- Create snapshot tables
   SELECT * INTO #temptable1 FROM [OriginalTable1];
   SELECT * INTO #temptable2 FROM [OriginalTable2];
   
   -- Your original script runs here
   UPDATE [OriginalTable1] SET ...
   
   -- Compare before/after using EXCEPT
   SELECT * INTO #temptable1Dif FROM [OriginalTable1]
   EXCEPT 
   SELECT * FROM #temptable1
   
   IF (SELECT COUNT(*) FROM #temptable1Dif) > 0
   BEGIN 
       SELECT * FROM [OriginalTable1] WHERE [ID] IN (SELECT ID FROM #temptable1Dif)
       SELECT * FROM #temptable1Dif
   END
   
   ROLLBACK TRANSACTION;
   ```
4. **Returns** the modified script as plain text

### Key Features
- ✅ **Non-destructive** - Always rolls back
- ✅ **Before/after comparison** - Shows exactly what changed
- ✅ **Table whitelist** - Only processes known tables
- ✅ **Multiple table support** - Handles scripts affecting multiple tables
- ⚠️ **Currently returns script only** - Database execution is commented out

---

## Technology Stack

### Core Framework
- **.NET 8.0** - Latest LTS version
- **C# 12** with nullable reference types enabled
- **ASP.NET Core** - Web API framework

### Database Access
- **Dapper** - Micro ORM for SQL queries
- **Microsoft.Data.SqlClient** - SQL Server provider
- **Polly** - Resilience and retry policies
  - 5 retries with 30-second delays between attempts

### Logging & Monitoring
- **Serilog** - Structured logging
  - Console sink (development)
  - HTTP sink to Logstash (production)
- **CorrelationId** - Request tracking across services
- **Zuto.SerilogExtensions** - Custom logging extensions

### API Documentation
- **Swashbuckle/Swagger** - OpenAPI documentation
- Auto-redirect from root to `/swagger`

### Containerization
- **Docker** - Multi-stage builds
  - Build stage: `mcr.microsoft.com/dotnet/sdk:8.0-jammy`
  - Runtime stage: `mcr.microsoft.com/dotnet/aspnet:8.0-jammy`
- **Note:** Deployment infrastructure currently non-functional

---

## Project Structure

```
SQL-Script-Flatten.API/
├── src/
│   ├── API/                          # Main API project
│   │   ├── Controllers/
│   │   │   └── ScriptController.cs   # HTTP endpoint
│   │   ├── Services/
│   │   │   ├── IScriptService.cs     # Service interface
│   │   │   └── ScriptService.cs      # Core business logic
│   │   ├── Ioc/
│   │   │   └── ServiceModule.cs      # DI registration
│   │   ├── DatabaseTables.cs         # Table whitelist (~600 tables)
│   │   ├── Program.cs                # Entry point, logging config
│   │   ├── Startup.cs                # Service configuration
│   │   └── ApplicationBuilderExtensions.cs
│   │
│   ├── Data/                         # Data access layer
│   │   ├── Dapper/
│   │   │   ├── DapperQuery.cs        # Dapper wrapper
│   │   │   ├── DbConnectionFactory.cs
│   │   │   ├── IDbConnectionFactory.cs
│   │   │   └── IQuery.cs
│   │   └── Repositories/
│   │       ├── IScriptRepository.cs
│   │       └── ScriptRepository.cs   # DB query execution
│   │
│   ├── BL/                           # ❌ Empty - ignore
│   └── Domain/                       # ❌ Empty - ignore
│
├── test/
│   └── Tests/                        # Test project
│
├── deploy/                           # ⚠️ Non-functional
├── Dockerfile                        # Container definition
├── SQL-Script-Flatten.API.sln       # Solution file
└── README.md
```

---

## Key Components

### 1. ScriptController (`src/API/Controllers/ScriptController.cs`)

```csharp
[Route("script")]
[HttpPost]
[Consumes(MediaTypeNames.Text.Plain)]
public async Task<ActionResult> PostScript()
```

- **Single endpoint** accepting plain text SQL
- Reads request body as stream
- Returns modified script as plain text response
- No authentication/authorization currently

### 2. ScriptService (`src/API/Services/ScriptService.cs`)

**Main Method:** `ScriptFlatten(string script)`

**Key Methods:**
- `GetTablesCalled(string script)` - Regex-based table extraction
- `CreateBeforeTables(Dictionary tables)` - Generates snapshot creation SQL
- `CreateComparison(Dictionary tables)` - Generates diff/comparison SQL

**Table Detection Regex:**
```csharp
@"\b(?:FROM|JOIN|INTO|UPDATE|DELETE\s+FROM)\s+((?:\[[^\]]+\]|\w+)(?:\.(?:\[[^\]]+\]|\w+))?)"
```

Extracts tables from:
- `FROM` clauses
- `JOIN` statements  
- `INSERT INTO`
- `UPDATE` statements
- `DELETE FROM`

### 3. DatabaseTables (`src/API/DatabaseTables.cs`)

Static whitelist of ~600 valid table names including:
- Schema-prefixed tables (e.g., `VLAPP.Applicants`, `AUTH.Users`)
- Simple table names (e.g., `Customers`, `Vehicles`)
- Case-insensitive comparison

**Purpose:** Security - prevents SQL injection by only processing known tables

### 4. ScriptRepository (`src/Data/Repositories/ScriptRepository.cs`)

```csharp
public async Task<Object> QueryDatabase(string script)
```

- Uses Dapper for query execution
- Wrapped in Polly retry policy
- **Currently commented out in ScriptService** - not being called

### 5. DapperQuery (`src/Data/Dapper/DapperQuery.cs`)

- Implements `IQuery` interface
- Wraps Dapper's `QueryAsync<T>` method
- Applies retry policy (5 attempts, 30s delays)
- Creates fresh connections per query

### 6. DbConnectionFactory (`src/Data/Dapper/DbConnectionFactory.cs`)

- Reads connection string from configuration
- Creates and opens SQL Server connections
- Connection string key: `"Horizon"`

---

## API Endpoints

### POST /script

**Description:** Submit SQL script for flattening/testing

**Request:**
- **Method:** POST
- **Content-Type:** `text/plain`
- **Body:** Raw SQL script

**Response:**
- **Content-Type:** `text/plain`
- **Body:** Modified SQL script with transaction wrapper

**Example:**

```bash
curl -X POST http://localhost:8080/script \
  -H "Content-Type: text/plain" \
  -d "UPDATE VLAPP.Applicants SET FirstName = 'Test' WHERE ID = 123"
```

**Response:**
```sql
BEGIN TRANSACTION;
SELECT * INTO #temptable1 FROM VLAPP.APPLICANTS;
UPDATE VLAPP.Applicants SET FirstName = 'Test' WHERE ID = 123
SELECT * INTO #temptable1Dif FROM VLAPP.APPLICANTS
EXCEPT 
SELECT * FROM #temptable1
IF(SELECT COUNT(*) FROM #temptable1Dif) > 0
BEGIN 
SELECT * FROM VLAPP.APPLICANTS WHERE [ID] IN (SELECT ID FROM #temptable1Dif)
SELECT * FROM #temptable1Dif END
ROLLBACK TRANSACTION;
```

### Other Endpoints

- **GET /** - Redirects to `/swagger`
- **GET /swagger** - API documentation UI
- **GET /health** - Health check endpoint

---

## Database Configuration

### Connection String Location
`src/API/appsettings.json`

```json
{
  "ConnectionStrings": {
    "Horizon": "Server=deathstar1.qa.zuto.cloud;Database=HorizonQA;User ID=HorizonUser;password=password$£2011;MultipleActiveResultSets=True;"
  }
}
```

### Database Details
- **Server:** deathstar1.qa.zuto.cloud
- **Database:** HorizonQA
- **Schema:** Multiple schemas (VLAPP, AUTH, DEALR, LEND, etc.)
- **Tables:** ~600 tables covering loan applications, dealers, lenders, etc.

### Environment-Specific Settings
- Development settings in `appsettings.Development.json`
- Environment detection: `ASPNETCORE_ENVIRONMENT` variable

---

## Script Processing Flow

```
1. HTTP POST → /script endpoint
   ↓
2. Read request body as string
   ↓
3. ScriptService.ScriptFlatten(script)
   ↓
4. Parse script with regex to find table names
   ↓
5. Validate tables against DatabaseTables.Tables whitelist
   ↓
6. Build output string:
   a. "BEGIN TRANSACTION;"
   b. CREATE snapshot tables (#temptable1, #temptable2, ...)
   c. INSERT original script
   d. CREATE comparison queries (EXCEPT logic)
   e. "ROLLBACK TRANSACTION;"
   ↓
7. Return modified script as plain text
```

### Example Transformation

**Input:**
```sql
UPDATE Customers SET Status = 'Active' WHERE ID = 5
```

**Output:**
```sql
BEGIN TRANSACTION;
SELECT * INTO #temptable1 FROM CUSTOMERS;
UPDATE Customers SET Status = 'Active' WHERE ID = 5
SELECT * INTO #temptable1Dif FROM CUSTOMERS
EXCEPT 
SELECT * FROM #temptable1
IF(SELECT COUNT(*) FROM #temptable1Dif) > 0
BEGIN 
SELECT * FROM CUSTOMERS WHERE [ID] IN (SELECT ID FROM #temptable1Dif)
SELECT * FROM #temptable1Dif END
ROLLBACK TRANSACTION;
```

---

## Important Notes

### Current State
- ✅ **API is functional** - Accepts scripts and returns modified versions
- ⚠️ **Database execution commented out** - Line 21 in ScriptService.cs
- ⚠️ **Deployment non-functional** - Infrastructure needs fixing
- ✅ **Local development works** - Can run via `dotnet run`

### Security Considerations
- **Table whitelist** prevents SQL injection by validating table names
- **No authentication** currently implemented
- **Connection string** hardcoded in appsettings.json (consider secrets management)
- **Direct SQL execution** (when uncommented) requires careful input validation

### Limitations
- Only detects tables in common SQL patterns (FROM, JOIN, UPDATE, etc.)
- Assumes all tables have an `[ID]` column for change detection
- No support for:
  - CREATE/DROP/ALTER statements
  - Stored procedure calls
  - Temporary table creation in user script
  - Cross-database queries

### Dependencies
Key NuGet packages:
- `Dapper` - Micro ORM
- `Polly` - Resilience
- `Serilog.AspNetCore` - Logging
- `Swashbuckle.AspNetCore` - Swagger/OpenAPI
- `CorrelationId` - Request tracking
- `Microsoft.Data.SqlClient` - SQL Server driver

### Configuration
- **Logging:** Configured in `Program.cs`
  - Development: Console only
  - Production: Console + Logstash HTTP endpoint
- **CORS:** Not configured
- **Health checks:** Mapped to `/health`
- **Swagger:** Available at `/swagger/index.html`

### Development Environment
- **IDE:** JetBrains Rider (based on open tabs)
- **Runtime:** .NET 8.0 SDK required
- **Database:** Requires access to HorizonQA database
- **Port:** Default 8080 (configured in Dockerfile)

---

## Quick Reference

### Running Locally
```bash
cd src/API
dotnet restore
dotnet run
```

### Testing the Endpoint
```bash
curl -X POST http://localhost:5000/script \
  -H "Content-Type: text/plain" \
  -d @test-script.sql
```

### Building Docker Image
```bash
docker build -t sql-script-flatten-api .
```

### Key Files to Modify
- **Add business logic:** `src/API/Services/ScriptService.cs`
- **Add endpoints:** `src/API/Controllers/ScriptController.cs`
- **Modify table whitelist:** `src/API/DatabaseTables.cs`
- **Change DI registrations:** `src/API/Ioc/ServiceModule.cs`
- **Update database logic:** `src/Data/Repositories/ScriptRepository.cs`

---

## Future Enhancements (Potential)

- [ ] Add authentication/authorization
- [ ] Support for stored procedures
- [ ] Better error handling and validation
- [ ] Support tables without ID columns
- [ ] Async/background job processing for long-running scripts
- [ ] Script execution history/audit log
- [ ] Web UI for script submission
- [ ] Support for multiple database environments
- [ ] Rate limiting and throttling
- [ ] Configurable retry policies

---

**Document End**
