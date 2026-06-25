# GMPPro20DataUpload Technical Design

## Abstract

This document represents the current-state technical design of the GMPPro20DataUpload framework.

Older design evolution notes are archived in `TechnicalDesign.V1.md`.

This document should be treated as the source of truth for future development, planning, and implementation.

The framework is designed to support multiple configurable Excel formats without collection-specific code changes:

- format configuration
- schema-driven mapping
- JSON templates
- lookup mappings
- computed fields
- insert/update processing
- MongoDB persistence
- processed Excel output with status/message/object ids


## GMPPro20DataUpload — Current-State Technical Design

---

### 1. Solution Structure

The solution is composed of three projects:

- **GMPPro20DataUpload.Models** — Plain data model classes with no external dependencies. Contains all shared DTOs: `SchemaRow`, `LookupMapping`, `FlowContext`, `ProcessingContext`, `ProcessResult`, `ValidationResult`, `ApplicationSettings`, `MongoConfiguration`.
- **GMPPro20DataUpload.Core** — Business logic. Contains all service interfaces (`Interfaces/`) and their implementations (`Services/`), plus the `SchemaColumnNames` constants and the `ServiceCollectionExtensions` DI registration helper.
- **GMPPro20DataUpload.UI** — Windows Forms host. Program.cs bootstraps configuration and DI; Form1.cs handles all user interaction.

Dependency direction is strictly: UI → Core → Models. Core never references UI.

---

### 2. Configuration

Configuration is loaded once at startup from appsettings.json and bound to two singleton objects registered with DI:

- **`MongoConfiguration`** — `ConnectionString` and `DatabaseName`, bound from the `MongoDB` section.
- **`ApplicationSettings`** — Bound from the `Application` section. Contains:
  - `CurrentModuleCode` — identifies the active upload module (e.g. `USERS`).
  - `ReferenceSuffix` — string appended to the formatted reference number to produce the final `referenceNumber` (e.g. `/00`).
  - `TemplateDirectory` — path to JSON templates, resolved to an absolute path anchored to the exe directory at startup.
  - `ModuleMappings` — dictionary mapping module codes to short prefixes used in request codes (e.g. `USERS → USR`).
  - `LookupMappings` — dictionary of named lookup definitions used during field resolution (see §6).

---

### 3. Schema Processing

The **Schema Excel** file drives the entire field-mapping pipeline. It is read by `ExcelService.ReadSchemaRows()` and parsed into a `List<SchemaRow>`. Each row represents one field in one MongoDB collection.

**`SchemaRow` columns:**

| Column | Description |
|---|---|
| `Collection` | MongoDB collection name |
| `Property` | Field identifier (used for value resolution and cache keying) |
| `DataType` | `text`, `integer`, `datetime`, `objectid`, or `object` |
| `IsMandatory` | Boolean; controls optional-lookup skip logic |
| `Source` | `excel`, `compute`, `auto`, `lookup`, or `update` |
| `Flow` | `publish` or `consume` (optional) |
| `FlowKey` | Key used to publish or consume a value in `FlowContext` |
| `JsonPath` | Full dot-notation path into the target JSON document |

`SchemaService` provides:
- `LoadSchema` — loads, filters blank rows, then validates all rows structurally (valid `DataType`, `Source`, `Flow`, non-empty `JsonPath`). Throws `InvalidOperationException` if violations are found.
- `GetCollectionOrder` — returns distinct collection names in first-appearance order from the schema file. This order is the exact processing sequence.
- `GetRowsForCollection`, `GetRowsBySource`, `GetFlowRows` — filtered views used by `ProcessingService`.

---

### 4. Template Processing

Each MongoDB collection has a corresponding JSON template file in the `TemplateDirectory`, named `{collectionName}.json` (e.g. `masterDesignations.json`).

`TemplateService` provides:
- `TemplateExists` — file existence check used during validation.
- `LoadTemplate` — reads the file as a raw string.
- `LoadTemplateAsNode` — parses the file into a `JsonNode` object graph, which `ProcessingService` mutates in-place during field resolution.

Templates serve as the starting-state document structure. All field values are written into the `JsonNode` tree using `SetValueAtJsonPath`, which navigates dot-notation paths and writes the final value as a `JsonValue`, correctly handling the `objectid` data type guard (throws if the value is empty).

---

### 5. Validation Flow

`ValidationService.ValidateAsync()` is called before any processing begins. It returns a `ValidationResult` (pass/fail + error list). The checks run in a defined order with early-exit guards:

1. **MongoDB connectivity** — `ping` command against the configured database.
2. **Data Excel existence and readability** — file must exist and `ReadDataRows` must succeed.
3. **`gender` column presence** — the data file must contain a `gender` header (required by the gender compute logic even if all values are blank).
4. **Schema file existence** — early exit if absent.
5. **Schema structural validity** — loaded via `SchemaService.LoadSchema`; any structural error is caught and surfaced.
6. **Schema not empty** — at least one valid row must exist.
7. **Template file existence** — one check per distinct collection in the schema.
8. **Lookup input column presence** — for every `InputType=excel` lookup mapping, the named column must exist in the data file unless the corresponding schema row is marked `IsMandatory=false`.

Processing is only enabled in the UI after this validation passes.

---

### 6. Lookup Processing

Lookup resolution is driven by the `LookupMappings` dictionary in `ApplicationSettings`. A schema row triggers lookup resolution when `Source=lookup`; its `FlowKey` is the key into that dictionary.

**`LookupMapping` properties:**
- `InputType`: `static` (fixed value) or `excel` (read from a named data-row column).
- `Collection` / `LookupPath`: the MongoDB collection and field path to match against.
- `OutputType`:
  - `objectid` — extracts a single field (`OutputPath`) from the found document and writes it as a scalar value.
  - `object` — applies a `Mappings` dictionary to populate multiple sub-properties at the target `JsonPath` node.

Lookup results are cached in `CacheService` using the key `lookup:{lookupKey}:{inputValue}` to avoid redundant MongoDB queries within a run.

For optional lookups (`IsMandatory=false`, `InputType=excel`), if the input column is absent or blank the lookup is silently skipped and the template value at that path is retained.

If a lookup fails (no matching document), an `InvalidOperationException` is thrown with a descriptive message that identifies the lookup key, collection, field, and search value.

---

### 7. Publish / Consume Flow

`FlowContext` is a per-run, case-insensitive key–value store attached to `ProcessingContext`. It enables values computed or resolved for one collection to be consumed by a later collection in the same row's processing cycle.

`FlowService` wraps `FlowContext` with `Publish`, `Consume`, `Exists`, and `Clear` operations.

**How publish/consume works in the schema:**
- A `SchemaRow` with `Flow=publish` causes its resolved value to be stored in `FlowContext` under `FlowKey` immediately after resolution.
- A `SchemaRow` with `Source=compute` and `Flow=consume` reads its value from `FlowContext` using `FlowKey` instead of computing it directly.

**Fixed publish events outside the schema:**
- After inserting `usrRequestBasicInfo`, its MongoDB `_id` is published as `requestId`.
- `requestCode` is also published if not already published by a schema row.

`FlowContext` is not cleared between rows within a run (cross-collection values such as `requestId` and `requestCode` persist for the entire run).

---

### 8. MongoDB Processing

`MongoService` is a singleton that lazily initialises a `MongoClient` and `IMongoDatabase` on first use.

**Operations:**

| Method | Behaviour |
|---|---|
| `TestConnectionAsync` | Creates a transient client and sends a `ping` command. |
| `FindOneAsync` | Case-insensitive anchored regex match on `fieldPath`. Returns JSON string or `null`. |
| `InsertAsync` | Parses JSON, inserts `BsonDocument`, returns the new `_id` as string. |
| `UpdateFieldAsync` | `$set` update by `_id`; writes as `BsonObjectId` when `dataType=objectid`, otherwise `BsonString`. |
| `GetActiveStatusIdAsync` | Queries `rootStatusMaster` for `statusCode=ACT`; result is cached on the singleton for the process lifetime. |
| `GetNextSequenceAsync` | Counts `usrRequestBasicInfo` documents with a matching `moduleCode` to derive the next sequence number. |

**Processing phases:**

- **Phase 1 (run-level):** `usrRequestBasicInfo` is built once — request code is generated, the document is inserted, and its `_id` is published to `FlowContext`.
- **Phase 2 (per-row):** For each data row, `ProcessingService` iterates the collection order (excluding `usrRequestBasicInfo`). For each collection, it checks for an existing document via `FindOneAsync` (with in-run cache). If found, the existing `_id` is reused and published via `auto` rows. If not found, the document is inserted, counters are incremented, `auto` rows receive the new `_id`, the cache is populated, and any `source=update` rows are applied with a post-insert `UpdateFieldAsync` call.

**Insert/duplicate counters** on `ProcessingContext`: `DesignationsInserted`, `UsersInserted`, `UsersDuplicate` (existing `masterUsers` records).

---

### 9. Excel Output Processing

`ExcelService.WriteOutputFile` produces the output Excel:

1. Copies the source data file to the destination path (`File.Copy` with overwrite).
2. Opens the copy with OpenXML in editable mode.
3. Reads the existing header row to determine the next available column index.
4. Appends `Status` and `Message` column headers to the header row.
5. Builds a dictionary of `rowNumber → ProcessResult` from `ctx.Results`.
6. Iterates all data rows by their `RowIndex` and appends inline string cells for `Status` and `Message` to each row that has a matching result.
7. Saves the worksheet part.

The output file is therefore the original data file with two extra columns appended, preserving all original data and formatting.

---

### 10. UI Layer

`Form1` is a Windows Forms dialog resolved from DI. It exposes:

- **File pickers** — `OpenFileDialog` for schema and data files; `SaveFileDialog` for output path.
- **Test Connection** button — calls `IMongoService.TestConnectionAsync` directly and reports the result to the status log.
- **Validate** button — calls `IValidationService.ValidateAsync`; lists all errors on failure or enables the Process button on success. Re-selecting a file resets validation state.
- **Process** button — only enabled after validation passes. Calls `IProcessingService.ProcessAsync` on a background task with an `IProgress<string>` callback and a `CancellationToken`. Progress messages drive both the status log and a percentage progress bar (parsed from "Processing row N of M" messages). On completion, a structured summary panel shows total/processed/success/failed/aborted counts, new-record counts per collection, duplicate counts, and a list of failed row numbers with messages.
- **Abort** button — cancels the `CancellationTokenSource`; processing finishes the current row before stopping.

## Format Configuration and Format Selection

### Overview

The application shall support multiple Excel upload formats through a centralized format configuration.

Users will select a format from a dropdown instead of manually selecting schema files.

Each format configuration defines the resources and settings required to process a specific Excel format.

### Format Configuration

A configuration file shall contain one entry for each supported format.

Each format entry shall contain:

* FormatKey
* DisplayName
* ModuleCode
* SchemaFile
* TemplateFile

Example:

```json
{
  "FormatKey": "MasterMaterials",
  "DisplayName": "Master Materials",
  "ModuleCode": "MAT",
  "SchemaFile": "MasterMaterialsSchema.xlsx",
  "TemplateFile": "masterMaterials.json"
}
```

### User Interface Changes

A new format selection dropdown shall be added.

Processing will not be allowed until a format is selected.

The selected format determines:

* Schema file to load
* JSON template file(s) to load
* ModuleCode used during processing

### Schema Loading

Schema files shall be loaded automatically from the selected format configuration.

Users will no longer browse and select schema files manually.

Validation rules:

* Format must be selected.
* SchemaFile must be configured.
* Schema file must exist.
* Schema file must be readable.

Processing shall stop if any validation fails.

### Template Loading

Template files shall be loaded automatically from the selected format configuration.

Validation rules:

* TemplateFile must be configured.
* Template file must exist.
* Template file must be readable.

Processing shall stop if any validation fails.

### ModuleCode Configuration

ModuleCode shall be obtained from the selected format configuration.

Schema rows using:

```text
Source = settings
FlowKey = moduleCode
```

shall receive the configured ModuleCode value.

This removes the need to hardcode module values in application configuration and allows different formats to use different module codes.

### Processing Flow

```text
User selects format
↓
Load format configuration
↓
Validate configuration
↓
Load schema from configuration
↓
Load template from configuration
↓
Load data Excel
↓
Execute validation
↓
Process data
```

## Generic Processing Summary and Messages

### Overview

The framework shall generate processing summaries and row messages without hardcoded references to specific formats or collections.

The summary shall be based on processing results only.

### Generic Processing Statistics

The framework shall maintain the following statistics during processing:

* Insert Count
* Duplicate Count
* Success Count
* Failed Count
* Warning Count

Statistics shall be generated dynamically from processing results.

### Generic Summary Generation

The processing summary shall not contain format-specific sections.

Current hardcoded summary labels shall be removed.

Examples:

* Users
* Designations

Summary values shall be generated directly from processing statistics.

### Generic Duplicate Messages

Duplicate messages shall not contain collection-specific wording.

Current messages such as:

```text
Duplicate user
```

shall be replaced with:

```text
Duplicate
```

### Generic Status Messages

Processing messages shall be reusable across all formats.

Examples:

```text
Inserted
Duplicate
Failed
Warning
```

The framework shall not contain format-specific status messages.

### Backward Compatibility

Processing behavior shall remain unchanged.

Only summary generation, statistics display, and user-facing messages shall be generalized.


## Duplicate Detection and Validation Framework

### Overview

The framework shall support configurable duplicate detection and validation rules through schema configuration.

Validation behavior shall not contain format-specific logic.

### Duplicate Detection Configuration

Duplicate detection shall be controlled through schema configuration.

Schema rows with:

```text
Source = key
```

shall be used to determine whether the target record already exists.

The framework shall use the configured JsonPath and schema property value to perform the existence check.

Behavior:

* If a matching record exists, processing shall stop for that row.
* Row status shall be marked as Duplicate.
* Record insertion shall be skipped.

### Key Source Validation

For schema rows using:

```text
Source = key
```

the framework shall validate:

* Property is specified.
* JsonPath is specified.
* Data column exists when required.
* Source configuration is valid.

Validation shall fail if any required configuration is missing.

### Filter Source Validation

For schema rows using:

```text
Source = filter
```

the framework shall validate:

* Property is specified.
* FlowKey is specified.
* Data column exists when required.
* Source configuration is valid.

Validation shall fail if any required configuration is missing.

### Generic Validation Framework

Validation shall be driven entirely by schema configuration.

Validation logic shall not contain format-specific rules.

The framework shall validate schema configuration before processing begins.

#### IsMandatory Validation

When:

```text
IsMandatory = TRUE
```

the framework shall validate:

* Required data column exists.
* Required value is provided.
* Required lookup value is resolved.
* Required source configuration is valid.

When:

```text
IsMandatory = FALSE
```

missing columns or missing values shall not cause validation failure unless required by the selected source type.

#### Source Validation

The framework shall validate source-specific requirements.

##### Source = excel

Validate:

* Property is specified.
* Excel column exists when mandatory.

##### Source = lookup

Validate:

* Lookup configuration exists.
* Required lookup settings are configured.
* Required lookup values are resolved.

##### Source = compute

Validate:

* Compute rule is supported.

##### Source = settings

Validate:

* FlowKey is specified.
* Requested setting exists.

##### Source = filter

Validate:

* FlowKey is specified.
* JsonPath is specified.
* Required data column exists when mandatory.

##### Source = key

Validate:

* JsonPath is specified.
* Required data column exists when mandatory.

##### Source = auto

Validate:

* Auto-generated value configuration is valid.

##### Source = formula

Validate:

* Formula is specified.
* Formula syntax is valid.
* Formula dependencies are available.

#### Formula Dependency Validation

For schema rows using:

```text
Source = formula
```

all referenced variables shall be defined in schema rows appearing before the formula row.

Validation shall fail when a formula depends on a value defined later in the schema.

#### Array Validation

Array processing shall follow IsMandatory rules.

When:

```text
IsMandatory = TRUE
```

all required lookup items must be resolved successfully.

When:

```text
IsMandatory = FALSE
```

unresolved lookup items shall be ignored and processing shall continue.

#### Validation Error Reporting

Validation failures shall include meaningful messages identifying:

* Property
* Source
* Collection
* Reason for failure

#### Format Independence

Validation logic shall not contain format-specific rules.

Examples of rules that shall not exist:

* User-specific validation
* Material-specific validation
* Department-specific validation
* Designation-specific validation

All validation behavior shall be derived from schema configuration.


### Null-Safe Parent Lookup Handling

Lookup processing shall safely handle missing parent objects.

If an expected parent object is null:

* Processing shall not throw an exception.
* Validation failure shall be reported with a meaningful message.
* Row processing shall continue for remaining rows.

### Multiple Lookup Match Handling

When a lookup returns multiple matching records:

* The first matching record shall be selected.
* Processing shall continue without failure.
* Additional matching records shall be ignored.

This behavior shall be applied consistently across all formats.

### Backward Compatibility

Existing lookup processing behavior shall remain unchanged wherever possible.

This enhancement focuses on:

* Configuration-driven duplicate detection.
* Configuration-driven validation.
* Safer lookup handling.
* Consistent multiple-match behavior.

## Dynamic Formula Support

### Overview

The framework shall support schema-driven formula evaluation.

Formula processing shall be configurable through schema without requiring code changes.

### Formula Source

Schema rows with:

```text
Source = formula
```

shall evaluate the expression defined in the Formula column.

### Formula Column

A new schema column shall be introduced:

```text
Formula
```

This column contains the formula expression to evaluate.

### Supported Syntax

Formula expressions shall support placeholder replacement using:

```text
{propertyName}
```

Values may reference:

* Current row values
* Previously computed values
* Published flow values
* System-generated values

### Formula Dependency Rules

A formula may reference only values that are available before the formula row is processed.

Validation shall ensure:

* Referenced property exists.
* Referenced property appears before the formula row.
* Referenced flow value exists.

Validation shall fail if dependency order is invalid.

### Formula Evaluation

Formula processing shall replace placeholders with resolved values and generate the final result.

The framework shall not contain format-specific formula logic.

### Initial Supported Formulas

The framework shall support existing formula scenarios including:

```text
{designationCode} - {designationName}
```

```text
{departmentCode} - {departmentName}
```

```text
{blockCode} - {blockName}
```

Advanced formatting such as {sno:00} is outside the current scope.

### Error Handling

Formula processing shall fail with meaningful messages when:

* Referenced property does not exist.
* Dependency order is invalid.
* Placeholder cannot be resolved.
* Formula syntax is invalid.

### Future Enhancements

Advanced functions, conditional expressions, mathematical operators, and custom functions are outside the scope of this version.


## Request Code Prefix Configuration

### Overview

Request code prefix generation shall be configuration-driven.

The framework shall not depend on hardcoded module prefix mappings.

### Format Configuration

Each format configuration shall define the request code prefix.

Example:

```text
Format Name    : USERS
Module Code    : USERS
Request Prefix : USR
```

### Request Code Generation

Request code generation shall use the configured request prefix from the selected format.

Example:

```text
USR1947
```

### Validation

Format configuration validation shall ensure:

* Request prefix is configured.
* Request prefix is not empty.
* Request prefix is unique when required.

---

## Generic Settings Configuration

### Overview

Settings-based values shall be configuration-driven.

The framework shall support resolving settings using schema configuration.

### Settings Resolution

Schema rows with:

```text
Source = settings
```

shall resolve values using the configured FlowKey.

Example:

```text
FlowKey = moduleCode
```

### Benefits

* Removes hardcoded setting resolution.
* Supports future format-level settings.
* Reduces code changes for new configuration values.

### Future Enhancements

Additional settings may be added as required.

---

## Legacy Code and Configuration Cleanup

### Overview

Unused code, configuration, and schema artifacts shall be removed after all formats are migrated and validated.

### Objective

Simplify maintenance and reduce unnecessary complexity.

### Examples

* Obsolete configuration entries.
* Unused schema columns.
* Deprecated processing logic.
* Unused application settings.

### Validation

Cleanup activities shall be performed only after confirming no active format depends on the functionality.
