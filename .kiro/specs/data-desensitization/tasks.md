# Implementation Plan: Data Desensitization

## Overview

Incrementally build a .NET 8 Blazor Server application for database desensitization. We start with core data models and provider abstractions, layer in strategy implementations and rule configuration, then build the execution pipeline, profile management, reporting, and finally the Blazor UI components — wiring everything together at the end.

## Tasks

- [x] 1. Set up project structure, data models, and core interfaces
  - Create a Blazor Server project targeting .NET 8
  - Add NuGet references: `Microsoft.Data.SqlClient`, `Npgsql`, `System.Text.Json`, `FsCheck`, `FsCheck.Xunit`, `xunit`
  - Create all data model records and enums (`DatabaseProvider`, `ConnectionStatus`, `ConnectionResult`, `TableInfo`, `ColumnInfo`, `MigrationRecord`, `DesensitizationStrategyType`, `StrategyParameters`, `DesensitizationRule`, `ValidationResult`, `Profile`, `ProfileLoadResult`, `ProfileImportResult`, `ProgressInfo`, `TableExecutionResult`, `ExecutionReport`, `PreviewResult`, `PreviewRow`)
  - Define all service interfaces (`IConnectionManager`, `ISchemaService`, `IDesensitizationStrategy`, `IRuleConfigurationService`, `IDesensitizationEngine`, `IProfileManager`, `IReportSerializer`, `IDbProviderFactory`, `ISchemaIntrospector`)
  - _Requirements: 1.1–1.6, 2.1–2.5, 3.1–3.5, 5.1–5.6, 6.1–6.8, 7.1–7.3, 8.1–8.4_

- [ ] 2. Implement provider abstraction and ConnectionManager
  - [x] 2.1 Implement `IDbProviderFactory` with `SqlServerProviderFactory` and `PostgreSqlProviderFactory`
    - Each factory creates a `DbConnection` for its provider
    - _Requirements: 1.4_

  - [x] 2.2 Implement `ConnectionManager`
    - `ConnectAsync` with 30-second timeout, returning `ConnectionResult` with database name and server address on success, or descriptive error on failure
    - `DisconnectAsync` closes connection and releases resources
    - `Status` property and `StatusChanged` event for UI binding
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 1.6_

  - [x] 2.3 Write unit tests for ConnectionManager
    - Test timeout enforcement, success/failure result construction, status transitions, disconnect cleanup
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 1.6_

- [ ] 3. Implement schema introspection and SchemaService
  - [x] 3.1 Implement `SqlServerSchemaIntrospector` and `PostgreSqlSchemaIntrospector`
    - Query `INFORMATION_SCHEMA` / `pg_catalog` for tables and columns
    - Implement `GetNewestMigrationAsync` to read `__EFMigrationsHistory`
    - _Requirements: 2.1, 2.2, 6.7_

  - [x] 3.2 Implement `SchemaService`
    - `GetTablesAsync`, `GetColumnsAsync`, `SearchTablesAsync` with case-insensitive name filtering
    - _Requirements: 2.1, 2.2, 2.4_

  - [x] 3.3 Write property test for schema filtering (Property 1)
    - **Property 1: Schema filter returns only matching items**
    - **Validates: Requirements 2.4**

  - [x] 3.4 Write unit tests for SchemaService
    - Test table/column retrieval with mocked introspector, search filtering edge cases
    - _Requirements: 2.1, 2.2, 2.4_

- [ ] 4. Implement desensitization strategies
  - [x] 4.1 Implement `RandomizationStrategy`
    - Generate random values matching column data type; respect `MinLength`/`MaxLength` for text columns
    - _Requirements: 3.1, 3.2_

  - [x] 4.2 Write property test for randomization bounds (Property 2)
    - **Property 2: Randomization respects length bounds**
    - **Validates: Requirements 3.2**

  - [x] 4.3 Implement `MaskingStrategy`
    - Replace characters with mask char, preserving `PreserveStart`/`PreserveEnd` characters
    - _Requirements: 3.1, 3.3_

  - [x] 4.4 Write property test for masking format (Property 3)
    - **Property 3: Masking preserves format**
    - **Validates: Requirements 3.3**

  - [x] 4.5 Implement `NullificationStrategy`, `FixedValueStrategy`, and `ShufflingStrategy`
    - Nullification returns `DBNull.Value` (only for nullable columns)
    - FixedValue returns user-specified constant with type validation
    - Shuffling collects and redistributes existing values
    - _Requirements: 3.1_

  - [x] 4.6 Write property test for strategy-type compatibility (Property 4)
    - **Property 4: Strategy-type compatibility is correctly validated**
    - **Validates: Requirements 3.5**

  - [x] 4.7 Write unit tests for all strategies
    - Test each strategy with concrete examples, edge cases, and type incompatibility scenarios
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 5. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Implement RuleConfigurationService and auto-detection
  - [x] 6.1 Implement `RuleConfigurationService`
    - `AddRule` with validation, `RemoveRule`, `Rules` property
    - `ValidateRule` checks strategy-column type compatibility
    - _Requirements: 2.3, 2.5, 3.5_

  - [x] 6.2 Implement `AutoDetectRules` in `RuleConfigurationService`
    - Scan column names against regex patterns (name, email, phone, address, ssn, credit_card, password)
    - Assign default strategies per detected category
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 6.3 Write property test for auto-detection classification (Property 5)
    - **Property 5: Auto-detection correctly classifies columns and assigns default strategies**
    - **Validates: Requirements 4.1, 4.3**

  - [x] 6.4 Write unit tests for RuleConfigurationService
    - Test AddRule validation, RemoveRule, AutoDetectRules with known column names
    - _Requirements: 2.3, 2.5, 3.5, 4.1, 4.3_

- [ ] 7. Implement DesensitizationEngine (execution and preview)
  - [x] 7.1 Implement `ExecuteAsync`
    - Validate at least one rule is configured before proceeding
    - Process each table in its own transaction; commit on success, rollback on error, continue to next table
    - Fire `ProgressChanged` events with current table, rows processed, total rows, estimated time remaining
    - Support cancellation via `CancellationToken` with rollback of uncommitted changes
    - Generate `ExecutionReport` on completion
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [x] 7.2 Implement `PreviewAsync`
    - Generate sample desensitized values for up to 10 rows without modifying the database
    - Return `PreviewResult` with original and desensitized value pairs
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 7.3 Write unit tests for DesensitizationEngine
    - Test execution flow, progress events, cancellation handling, report generation, preview output
    - Mock database layer
    - _Requirements: 5.1–5.6, 7.1–7.3_

- [ ] 8. Implement ProfileManager
  - [x] 8.1 Implement `SaveProfileAsync` and `LoadProfileAsync`
    - Persist profiles as named JSON files in local storage
    - On load, match rules to current schema by table/column name; partition into matched and unmatched
    - _Requirements: 6.1, 6.2, 6.3, 6.5_

  - [x] 8.2 Implement `ExportProfileAsync` and `ImportProfileAsync`
    - Export includes connection string and newest `__EFMigrationsHistory` record
    - Import reads JSON, queries target database migration record, blocks import on mismatch
    - _Requirements: 6.4, 6.6, 6.7, 6.8_

  - [x] 8.3 Write property test for profile-schema rule matching (Property 6)
    - **Property 6: Profile-schema rule matching partitions correctly**
    - **Validates: Requirements 6.2, 6.3**

  - [x] 8.4 Write property test for profile JSON round-trip (Property 7)
    - **Property 7: Profile JSON round-trip**
    - **Validates: Requirements 6.4, 6.5, 6.6**

  - [x] 8.5 Write property test for migration record comparison (Property 8)
    - **Property 8: Migration record comparison**
    - **Validates: Requirements 6.8**

  - [x] 8.6 Write unit tests for ProfileManager
    - Test save/load, export/import JSON structure, migration mismatch blocking, unmatched rule warnings
    - _Requirements: 6.1–6.8_

- [ ] 9. Implement ReportSerializer
  - [x] 9.1 Implement `ReportSerializer`
    - Serialize `ExecutionReport` to JSON using configured `System.Text.Json` options (camelCase, indented, enum as string, ignore null)
    - Deserialize JSON back to `ExecutionReport`
    - `ExportToFileAsync` and `ImportFromFileAsync` for file I/O
    - Wrap `JsonException` in domain-specific `ReportParsingException`
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x] 9.2 Write property test for ExecutionReport JSON round-trip (Property 9)
    - **Property 9: ExecutionReport JSON round-trip**
    - **Validates: Requirements 8.3**

  - [x] 9.3 Write unit tests for ReportSerializer
    - Test serialization output format, deserialization of known JSON, error handling for malformed JSON
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [x] 10. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 11. Build Blazor UI — Connection and Schema components
  - [x] 11.1 Create Connection Management page
    - Connection string input, database provider dropdown (SQL Server, PostgreSQL), Connect/Disconnect buttons
    - Display confirmation message with database name and server address on success
    - Display descriptive error message on failure
    - Show connection status in application header
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [x] 11.2 Create Schema Browser component
    - Tree view of tables with expandable column details (name, data type, nullability)
    - Text search input for filtering tables and columns by name
    - Visual distinction for columns with assigned desensitization rules
    - Highlight auto-detected sensitive columns
    - _Requirements: 2.1, 2.2, 2.4, 2.5, 4.2_

- [ ] 12. Build Blazor UI — Rule Configuration and Preview components
  - [x] 12.1 Create Rule Configuration Panel
    - Strategy selection dropdown per selected column
    - Strategy-specific parameter inputs (min/max length for Randomization, mask char and preserve counts for Masking, fixed value input for FixedValue)
    - Validation error display for incompatible strategy-column combinations
    - Accept/modify/dismiss controls for auto-detected suggestions
    - _Requirements: 2.3, 3.1, 3.2, 3.3, 3.4, 3.5, 4.4_

  - [x] 12.2 Create Data Preview component
    - Side-by-side comparison of original and desensitized values for up to 10 rows
    - Invalidate preview when rules are modified
    - _Requirements: 7.1, 7.2, 7.3_

- [ ] 13. Build Blazor UI — Execution, Profile, and Report components
  - [x] 13.1 Create Execution Progress component
    - Start button with validation (at least one rule configured)
    - Progress indicator showing current table, rows processed, estimated time remaining
    - Cancel button to abort with rollback
    - Display execution report on completion
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [x] 13.2 Create Profile Management component
    - Save/load profiles by name
    - Export/import profile JSON files
    - Warning display for unmatched rules on load
    - Error display for schema version mismatch on import
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8_

  - [x] 13.3 Create Execution Report component
    - Display report details (rows updated per table, elapsed time, errors)
    - Export report as JSON to user-specified location
    - Import and display previously exported reports
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [x] 14. Wire up DI registration and navigation
  - Register all services in `Program.cs` (`IConnectionManager`, `ISchemaService`, `IRuleConfigurationService`, `IDesensitizationEngine`, `IProfileManager`, `IReportSerializer`, `IDbProviderFactory`, `ISchemaIntrospector`)
  - Configure navigation routes for all pages
  - Ensure all components are connected to their backing services
  - _Requirements: 1.1–8.4_

- [x] 15. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck
- Unit tests validate specific examples and edge cases
