# DataDesensitization

When debugging production issues, you often need to copy prod data into a non-production environment. But prod data contains sensitive information (PII, credentials, etc.) that shouldn't live in dev/test databases. This tool desensitizes that data after it's been copied — so you can debug and test with realistic data without exposing anything sensitive.

Connect to SQL Server or PostgreSQL, define desensitization rules, preview the results, and execute — all from a clean browser UI built with ASP.NET Core 8.0 Blazor Server and Tailwind CSS.

## Features

- **SQL Server & PostgreSQL** support out of the box
- **5 desensitization strategies**: Randomization, Masking, Nullification, Fixed Value, Shuffling
- **Schema browser** with auto-detection of sensitive columns (email, phone, SSN, credit card, etc.)
- **Rule validation** with foreign key protection to maintain referential integrity
- **Live preview** of desensitized data before committing changes
- **Real-time progress tracking** during execution with estimated time remaining
- **Execution reports** exported as JSON with per-table results
- **Profile management** — export/import rule configurations as JSON files (Entity Framework databases only)

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A SQL Server or PostgreSQL database

## Getting Started

```bash
# Clone the repository
git clone https://github.com/<your-username>/DataDesensitization.git
cd DataDesensitization

# Restore and run
dotnet run --project src/DataDesensitization
```

The app will start at `https://localhost:5001` (or the port shown in the console).

## User Guide

### 1. Connect to a Database

Navigate to **Connection** from the sidebar. Enter your connection string, select the database provider (SQL Server or PostgreSQL), and click **Connect**.

Example connection strings:

| Provider   | Example                                                                 |
|------------|-------------------------------------------------------------------------|
| SQL Server | `Server=localhost;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True` |
| PostgreSQL | `Host=localhost;Database=mydb;Username=postgres;Password=secret`         |

Once connected, a green **Connected** badge appears in the top bar and all other pages become accessible.

### 2. Browse the Schema

Go to **Schema Browser** to explore your database tables and columns. Click any table to expand its columns. The browser shows:

- Column name, data type, nullability, and max length
- **Rule Assigned** badge for columns that already have a desensitization rule
- **Sensitive** badge for columns whose names match common PII patterns (name, email, phone, address, SSN, credit card, password)

Use the search bar to filter tables and columns.

### 3. Configure Rules

On the **Rules** page you can:

- **Add rules manually** — pick a table, column, and strategy. The form provides autocomplete suggestions from your schema and blocks foreign key columns to protect referential integrity.
- **Auto-detect** — click "Auto-Detect Sensitive Columns" to scan the entire schema and get rule suggestions based on column naming patterns. Accept or dismiss each suggestion individually.

#### Desensitization Strategies

| Strategy       | Description                                      | Parameters                          |
|----------------|--------------------------------------------------|-------------------------------------|
| Randomization  | Replace with random characters                   | Min Length, Max Length               |
| Masking        | Mask characters while preserving start/end       | Mask Character, Preserve Start, Preserve End |
| Nullification  | Set the value to `NULL`                          | —                                   |
| Fixed Value    | Replace with a constant string                   | Fixed Value                         |
| Shuffling      | Shuffle values across rows within the column     | —                                   |

### 4. Preview Changes

Go to **Preview**, enter a table name, and click **Generate Preview**. The page shows a side-by-side comparison of original vs. desensitized values for up to 10 rows. If rules change after generating a preview, a stale-data warning appears prompting you to refresh.

### 5. Execute Desensitization

On the **Execute** page, click **Start Desensitization** to apply all configured rules to the live database. During execution you'll see:

- Current table being processed
- Progress bar with row count and percentage
- Estimated time remaining
- A **Cancel** button that rolls back uncommitted changes

When complete, an execution report is displayed with per-table row counts, elapsed time, and error details. The report is also automatically downloaded as a JSON file.

### 6. Manage Profiles

The **Profiles** page lets you save and restore rule configurations:

- **Export** — downloads the current rules and Entity Framework migration history as a JSON file.
- **Import** — uploads a previously exported profile. The migration history is validated against the connected database to ensure compatibility. Rules that reference columns no longer in the schema are reported as warnings.

> **Note:** Profile management requires a database generated by Entity Framework. Migration history is used to validate that profiles are compatible with the target database.

## Project Structure

```
src/DataDesensitization/
├── Components/
│   ├── Layout/          # MainLayout, NavMenu
│   └── Pages/           # Razor pages (Home, Connection, Schema, Rules, Preview, Execute, Profiles)
├── Models/              # Domain models (DesensitizationRule, Profile, ExecutionReport, etc.)
├── Services/            # Business logic (ConnectionManager, DesensitizationEngine, SchemaService, etc.)
├── Exceptions/          # Custom exceptions
└── Program.cs           # App entry point and DI configuration
```

## License

This project is licensed under the [MIT License](LICENSE).
