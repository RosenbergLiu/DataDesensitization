# Requirements Document

## Introduction

A .NET 8 Blazor application that enables database administrators and developers to desensitize sensitive data stored in relational databases. The application connects to a target database, allows users to identify columns containing sensitive information (such as names, emails, phone numbers, addresses, and financial data), and replaces those values with meaningless or randomized data. This ensures that non-production environments can use realistic database structures without exposing real personal or confidential information.

## Glossary

- **Desensitization_Engine**: The core processing component responsible for generating replacement values and executing update operations against the target database.
- **Connection_Manager**: The component responsible for establishing, validating, and managing database connections.
- **Rule_Configuration_Panel**: The Blazor UI component where users define which columns to desensitize and which desensitization strategy to apply.
- **Desensitization_Rule**: A user-defined mapping that associates a specific database column with a desensitization strategy.
- **Desensitization_Strategy**: A named algorithm for generating replacement data (e.g., randomization, masking, nullification, fixed value).
- **Schema_Browser**: The UI component that displays the database schema (tables and columns) for user selection.
- **Execution_Report**: A summary generated after a desensitization run, detailing the number of rows and columns processed, any errors encountered, and the elapsed time.
- **Sensitive_Column**: A database column identified by the user or by auto-detection as containing personally identifiable information or other confidential data.
- **Profile**: A saved collection of desensitization rules, along with schema version metadata, that can be reused across multiple desensitization runs.
- **__EFMigrationsHistory**: The standard Entity Framework Core table that tracks applied database migrations. Each record contains a MigrationId and ProductVersion, with the newest record representing the current schema version of the database.

## Requirements

### Requirement 1: Database Connection Management

**User Story:** As a database administrator, I want to connect to various relational databases, so that I can desensitize data across different database systems.

#### Acceptance Criteria

1. WHEN a user provides a connection string and selects a database provider, THE Connection_Manager SHALL attempt to establish a connection to the target database within 30 seconds.
2. WHEN the connection is successfully established, THE Connection_Manager SHALL display a confirmation message including the database name and server address.
3. IF the connection attempt fails, THEN THE Connection_Manager SHALL display a descriptive error message indicating the cause of the failure (e.g., invalid credentials, unreachable host, unsupported provider).
4. THE Connection_Manager SHALL support at minimum SQL Server and PostgreSQL database providers.
5. WHILE a connection is active, THE Connection_Manager SHALL display the connection status in the application header.
6. WHEN a user requests to disconnect, THE Connection_Manager SHALL close the database connection and release all associated resources.

### Requirement 2: Schema Browsing and Column Selection

**User Story:** As a developer, I want to browse the database schema and select columns for desensitization, so that I can precisely target sensitive data.

#### Acceptance Criteria

1. WHEN a database connection is active, THE Schema_Browser SHALL retrieve and display all user tables and their columns from the connected database.
2. WHEN a user expands a table node, THE Schema_Browser SHALL display each column with its name, data type, and nullability.
3. WHEN a user selects one or more columns, THE Rule_Configuration_Panel SHALL allow the user to assign a Desensitization_Strategy to each selected column.
4. THE Schema_Browser SHALL allow the user to filter tables and columns by name using a text search input.
5. THE Schema_Browser SHALL visually distinguish columns that already have a Desensitization_Rule assigned.

### Requirement 3: Desensitization Strategy Configuration

**User Story:** As a database administrator, I want to choose from multiple desensitization strategies, so that I can apply the appropriate transformation for each type of sensitive data.

#### Acceptance Criteria

1. THE Desensitization_Engine SHALL provide the following built-in strategies: Randomization (generate random values matching the column data type), Masking (replace characters with a mask character while preserving format), Nullification (set the column value to NULL), Fixed Value (replace with a user-specified constant value), and Shuffling (redistribute existing values randomly across rows).
2. WHEN a user selects the Randomization strategy for a text column, THE Rule_Configuration_Panel SHALL allow the user to specify a minimum and maximum length for the generated value.
3. WHEN a user selects the Masking strategy, THE Rule_Configuration_Panel SHALL allow the user to specify the mask character and the number of characters to preserve from the start or end of the original value.
4. WHEN a user selects the Fixed Value strategy, THE Rule_Configuration_Panel SHALL require the user to provide the replacement value.
5. IF a user assigns a strategy that is incompatible with the target column data type, THEN THE Rule_Configuration_Panel SHALL display a validation error describing the incompatibility.

### Requirement 4: Sensitive Column Auto-Detection

**User Story:** As a developer, I want the application to suggest which columns might contain sensitive data, so that I can save time and reduce the risk of missing sensitive fields.

#### Acceptance Criteria

1. WHEN a user triggers auto-detection on a connected database, THE Desensitization_Engine SHALL analyze column names and suggest columns likely to contain sensitive data based on common naming patterns (e.g., columns containing "name", "email", "phone", "address", "ssn", "credit_card", "password").
2. WHEN auto-detection completes, THE Schema_Browser SHALL highlight the suggested Sensitive_Columns and pre-select them for desensitization.
3. THE Desensitization_Engine SHALL assign a default Desensitization_Strategy to each auto-detected Sensitive_Column based on the detected data category.
4. WHEN a user reviews auto-detected suggestions, THE Rule_Configuration_Panel SHALL allow the user to accept, modify, or dismiss each suggestion individually.

### Requirement 5: Desensitization Execution

**User Story:** As a database administrator, I want to execute the desensitization process and monitor its progress, so that I can ensure the operation completes successfully.

#### Acceptance Criteria

1. WHEN a user initiates a desensitization run, THE Desensitization_Engine SHALL validate that at least one Desensitization_Rule is configured before proceeding.
2. WHEN a desensitization run begins, THE Desensitization_Engine SHALL display a progress indicator showing the current table, the number of rows processed, and the estimated time remaining.
3. THE Desensitization_Engine SHALL process each table within a database transaction, committing only after all rows in that table are successfully updated.
4. IF an error occurs during desensitization of a table, THEN THE Desensitization_Engine SHALL roll back the transaction for that table, log the error, and continue processing remaining tables.
5. WHEN a desensitization run completes, THE Desensitization_Engine SHALL generate an Execution_Report containing the total rows updated per table, the total elapsed time, and any errors encountered.
6. WHILE a desensitization run is in progress, THE Desensitization_Engine SHALL allow the user to cancel the operation, rolling back any uncommitted changes.

### Requirement 6: Profile Management

**User Story:** As a database administrator, I want to save and load desensitization configurations as profiles, so that I can reuse them across multiple runs and environments.

#### Acceptance Criteria

1. WHEN a user saves the current set of Desensitization_Rules, THE Rule_Configuration_Panel SHALL persist the configuration as a named Profile in local storage or a JSON file.
2. WHEN a user loads a saved Profile, THE Rule_Configuration_Panel SHALL apply all stored Desensitization_Rules to the current schema, matching rules to columns by table and column name.
3. IF a loaded Profile references columns that do not exist in the current database schema, THEN THE Rule_Configuration_Panel SHALL display a warning listing the unmatched rules.
4. THE Rule_Configuration_Panel SHALL allow the user to export a Profile as a JSON file and import a Profile from a JSON file.
5. THE Profile SHALL store the following for each rule: table name, column name, selected Desensitization_Strategy, and all strategy-specific parameters.
6. WHEN a user exports a Profile as a JSON file, THE Rule_Configuration_Panel SHALL include the database connection string and the newest record from the __EFMigrationsHistory table (MigrationId and ProductVersion) in the exported JSON.
7. WHEN a user imports a Profile from a JSON file, THE Rule_Configuration_Panel SHALL query the target database's __EFMigrationsHistory table and compare the newest migration record against the migration record stored in the JSON file.
8. IF the newest migration record in the target database's __EFMigrationsHistory table does not match the migration record stored in the imported Profile JSON, THEN THE Rule_Configuration_Panel SHALL block the import and display an error message indicating the schema version mismatch between the Profile and the target database.

### Requirement 7: Data Preview

**User Story:** As a developer, I want to preview the desensitization results before committing changes, so that I can verify the output meets expectations.

#### Acceptance Criteria

1. WHEN a user requests a preview for a configured table, THE Desensitization_Engine SHALL generate sample desensitized values for up to 10 rows without modifying the database.
2. THE Schema_Browser SHALL display the preview in a side-by-side comparison showing original values and their desensitized replacements.
3. WHEN a user modifies a Desensitization_Rule after previewing, THE Desensitization_Engine SHALL invalidate the previous preview for the affected columns.

### Requirement 8: Execution Report Serialization

**User Story:** As a database administrator, I want to export execution reports, so that I can maintain an audit trail of desensitization operations.

#### Acceptance Criteria

1. WHEN a desensitization run completes, THE Desensitization_Engine SHALL serialize the Execution_Report to JSON format.
2. THE Desensitization_Engine SHALL parse a previously exported JSON Execution_Report back into an Execution_Report object for display.
3. FOR ALL valid Execution_Report objects, serializing to JSON then parsing back SHALL produce an equivalent Execution_Report object (round-trip property).
4. WHEN a user requests an export, THE Desensitization_Engine SHALL save the Execution_Report JSON file to a user-specified location.
