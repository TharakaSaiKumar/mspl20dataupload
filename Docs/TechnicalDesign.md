# GMPPro20DataUpload - Technical Design

## Project Overview

GMPPro20DataUpload is a .NET 8 application that processes Excel-based master data and generates MongoDB documents using a schema-driven architecture.

The framework must be configuration-driven and support future collection additions with minimal code changes.

---

# Architecture Rules

## Important

Do NOT redesign the architecture.

Generate code according to this document.

The framework is schema-driven.

Business rules are controlled by:

* Schema Excel
* Data Excel
* Application-deployed JSON Templates

---

# Solution Structure

```text
GMPPro20DataUpload.sln

├── GMPPro20DataUpload.UI
│    (.NET 8 WinForms)
│
├── GMPPro20DataUpload.Core
│    (Business Logic)
│
├── GMPPro20DataUpload.Models
│    (Models)
│
├── Templates
│
└── Logs
```

---

# Project References

```text
UI
 ↓
Core
 ↓
Models
```

Rules:

* UI references Core
* Core references Models
* Models references nobody

---

# Current Collections

## usrRequestBasicInfo

Purpose:

* Upload tracking
* Request generation

---

## masterDesignations

Purpose:

* Designation creation
* Designation reuse

---

## masterUsers

Purpose:

* User creation
* User reuse

---

# Input Files

## User Uploaded Files

### Data Excel

Current columns:

```text
designationCode
designationName
employeeID
userName
```

### Schema Excel

Current columns:

```text
Collection
Property
JsonPath
Source
Type
IsMandatory
Flow
FlowKey
```

---

## Application Files

```text
usrRequestBasicInfo.json
masterDesignations.json
masterUsers.json
```

These files are deployed with the application.

Users do not upload them.

---

# Schema Processing Rules

## Processing Order

Process schema rows in the exact order they appear in Excel.

No Sequence column.

No sorting.

Example:

```text
Row 1
Row 2
Row 3
```

must be processed in the same order.

---

# Source Types

## Excel

Value comes from Excel.

Examples:

```text
designationName
designationCode
employeeID
userName
```

---

## Compute

Value generated before insert.

Examples:

```text
createdOn
updatedOn
sno
requestCode
statusID
formattedReferenceNumber
referenceNumber
formattedName
```

---

## Auto

Value obtained after lookup or insert.

Examples:

```text
_id
```

---

# JsonPath Rules

Always use full JsonPath.

Examples:

```text
systemData.createdOn
systemData.requestCode
designations.designationName
userDetails.designation.itemID
_id
isSynced
```

Do not concatenate path and property.

JsonPath already contains full destination path.

---

# Flow Rules

Flow allows sharing data between collections.

Supported actions:

## Publish

Store value in flow.

## Consume

Read value from flow.

Examples:

```text
requestCode
designationId
designationCode
designationName
designationFormattedName
```

---

# Flow Keys

Use unique names.

Good:

```text
requestCode
designationId
designationName
designationFormattedName
```

Avoid:

```text
id
name
status
```

---

# Cache Rules

Use in-memory cache.

Purpose:

* Reduce MongoDB calls
* Avoid repeated existence checks
* Improve performance

Processing order must not depend on Excel sorting.

Cache is preferred over complex multi-level loops.

---

# Existing Record Rules

If record exists:

* Reuse existing record
* Reuse existing _id
* Reuse existing flow values

Do NOT update:

```text
excelFilename
rowNumber
```

These fields represent original creation metadata.

---

# Traceability Fields

All newly inserted records contain:

```text
excelFilename
rowNumber
```

Stored at root level.

Purpose:

* Audit
* Troubleshooting
* Source tracking

---

# Request Code Generation

Formula:

```text
moduleCode + next sequence number
```

Example:

```text
ORG01
ORG02
ORG03
```

---

# Sequence Formatting Rules

Minimum 2-digit formatting.

Examples:

```text
1   -> 01
2   -> 02
9   -> 09
10  -> 10
99  -> 99
100 -> 100
```

Only numbers below 10 receive leading zero.

---

# Reference Number Rules

## formattedReferenceNumber

Formula:

```text
requestCode + "-" + sno
```

Examples:

```text
ORG-01
ORG-10
ORG-100
```

---

## referenceNumber

Formula:

```text
formattedReferenceNumber + "/01"
```

Examples:

```text
ORG-01/01
ORG-10/01
ORG-100/01
```

---

# Status Rules

StatusID comes from:

```text
rootStatusMaster
```

Condition:

```text
statusCode = ACT
```

---

# Common System Values

```text
createdBy = system
updatedBy = system
formState = ACTIVE
status = Active
```

Values come from JSON templates.

---

# Validation Rules

Before processing:

1. Validate MongoDB connection
2. Validate schema file
3. Validate JSON templates
4. Validate Excel file

Stop processing if validation fails.

---

# Excel Processing Rules

Trim all Excel values.

Examples:

```text
" ADM"    -> "ADM"
"Admin "  -> "Admin"
" 12345 " -> "12345"
```

---

# Duplicate Matching

Case-insensitive.

Examples:

```text
ADM = adm
Manager = manager
```

Treat as same value.

---

# Output File

Keep original Excel unchanged.

Create processed copy.

Add columns:

```text
Status
Message
```

After processing:

Open Save As dialog.

Suggested filename:

```text
originalfilename_processed.xlsx
```

Example:

```text
employees.xlsx
employees_processed.xlsx
```

---

# Progress Reporting

Display:

```text
Processing row X of Y
```

during processing.

---

# Abort Processing

If user aborts:

* Finish current row
* Save current progress
* Stop remaining rows

No abrupt termination.

---

# Future Collections

Framework must support future collections through:

* Schema updates
* JSON template updates

Examples:

```text
Departments
Units
Locations
Roles
```

Avoid hardcoding collection-specific logic where possible.

---

# Development Goal

Build reusable framework components.

Future UI may change:

```text
WinForms
Web API
Angular
Blazor
```

Core processing logic should remain reusable.

# Request Code Generation

`usrRequestBasicInfo.requestCode` will be generated dynamically for each upload.

Formula:

moduleCode + next sequence number

The next sequence number will be calculated based on existing records for the same moduleCode in `usrRequestBasicInfo`.

Minimum 2-digit formatting will be applied.

Examples:

DE + 1  = DE01  
DE + 2  = DE02  
DE + 9  = DE09  
DE + 10 = DE10  
DE + 99 = DE99  
DE + 100 = DE100

## Excel Library Abstraction

Excel access must be handled only through IExcelService.

The current implementation will use ClosedXML for reading and writing Excel files.

Microsoft Office Interop should not be used because it requires Excel installation, complicates deployment, and is not suitable for future Web API/server-side execution.

If the Excel library changes in the future, only ExcelService should be updated.

## Excel Library Decision

The application will use OpenXML SDK for Excel processing.

Reason:
- Existing company projects already use OpenXML.
- Team familiarity and consistency.
- No dependency on Microsoft Excel installation.

Excel access remains abstracted through IExcelService.
If the underlying Excel library changes in the future, only ExcelService should require modification.

## V1 Update - Request Code and Reference Number Rules

### usrRequestBasicInfo

* `sno` is removed from the schema.
* `sno` remains in the JSON template with a fixed value of `1`.
* `requestCode` is generated dynamically once per upload.

#### Module Mapping Configuration

```text
USERS        -> USR
DESIGNATIONS -> DSG
DEPARTMENTS  -> DEP
```

#### Formula

```text
requestCode = ModuleMappings[moduleCode] + formattedSequence
```

#### Sequence Rule

```text
sequence = count of usrRequestBasicInfo records for the selected moduleCode + 1
```

#### Formatting Rules

```text
1   -> 01
9   -> 09
10  -> 10
100 -> 100
123 -> 123
```

#### Example

```text
moduleCode = USERS
prefix = USR

sequence = 1

requestCode = USR01
```

Additional examples:

```text
USR01
USR09
USR100
USR123
```

---

### masterDesignations and masterUsers

* `sno` is removed from the schema.
* `sno` remains in the JSON templates with a fixed value of `1`.
* `requestCode` uses the same upload requestCode generated in `usrRequestBasicInfo`.

#### Formula

```text
formattedReferenceNumber = requestCode + "-" + formattedInsertSequence

referenceNumber = formattedReferenceNumber + referenceSuffix
```

#### Configuration

```text
referenceSuffix = /00
```

#### Formatting Rules

```text
1  -> 01
9  -> 09
10 -> 10
25 -> 25
```

#### Example

```text
requestCode = USR01

1st inserted designation:
formattedReferenceNumber = USR01-01
referenceNumber = USR01-01/00

25th inserted designation:
formattedReferenceNumber = USR01-25
referenceNumber = USR01-25/00
```

#### Rules

* Designation insert sequence and user insert sequence are maintained separately.
* Increment the sequence only when a new record is inserted.
* Do not increment the sequence when an existing record is reused.
* The same rules apply to both `masterDesignations` and `masterUsers`.

V1:
- CurrentModuleCode is configured in appsettings.json.
- ProcessingService reads CurrentModuleCode and resolves the prefix using ModuleMappings.
- requestCode is generated dynamically during processing.

Future:
- CurrentModuleCode will be selected from a UI dropdown.
- ProcessingService logic will remain unchanged.

usrRequestBasicInfo._id is published using flow key "requestId".

masterDesignations.systemData.basicInfoID
and
masterUsers.systemData.basicInfoID

consume the "requestId" flow value and store it as an ObjectId reference.

## Future Enhancement - Explicit Lookup Keys

### Current V1 Behaviour

ProcessingService determines whether a document already exists by using the first schema row with:

```text
source = excel
```

for the collection.

Current schema ordering results in:

```text
masterDesignations -> designationCode
masterUsers        -> employeeID
```

being used as lookup keys.

### Limitation

The lookup behaviour depends on schema row ordering.

If schema rows are reordered in the future, the existence check could unintentionally use a different field.

### Future Enhancement

Add an explicit schema column:

```text
LookupKey
```

Example:

```text
Collection           Property         LookupKey
masterDesignations   designationCode  TRUE
masterDesignations   designationName  FALSE

masterUsers          employeeID       TRUE
masterUsers          userName         FALSE
```

ProcessingService would then use the row marked as `LookupKey=TRUE` rather than relying on schema order.

### Status

Deferred for V1.

Current implementation remains unchanged.

## Master Users - Additional V1 Fields

### User Lookup Key

`masterUsers` existence check is currently based on the first `source=excel` row in the schema.

For V1, `userLoginID` must be the first `source=excel` row for `masterUsers`.

Current lookup rule:

```text
masterUsers -> userDetails.userLoginID

Future enhancement:

Add explicit LookupKey column to schema.
New User Excel Columns

The data template includes these new columns:

gender
dateOfJoining
officialEmail
userLoginID

Rules:

Column	Required	Output
userLoginID	Yes	userDetails.userLoginID
gender	No	userDetails.gender
dateOfJoining	No	userDetails.dateOfJoining
officialEmail	No	userDetails.officialEmail
Gender Transformation

Source:

Excel column: gender
Schema source: compute
Schema DataType: object

If gender is blank:

"gender": null

If gender is male, MALE, or Male:

{
  "itemID": "MALE",
  "itemCode": "MALE",
  "item": "Male",
  "itemType": null,
  "isActive": null,
  "systemCode": null,
  "extraInfo": null,
  "displayData": "Male"
}

If gender is female, FEMALE, or Female:

{
  "itemID": "FEMALE",
  "itemCode": "FEMALE",
  "item": "Female",
  "itemType": null,
  "isActive": null,
  "systemCode": null,
  "extraInfo": null,
  "displayData": "Female"
}

Transformation rules:

Property	Rule
itemID	Uppercase gender value
itemCode	Uppercase gender value
item	Sentence case gender value
displayData	Sentence case gender value
itemType	null
isActive	null
systemCode	null
extraInfo	null

Validation note:

Although gender is source=compute, it depends on the Excel column gender. Validation must confirm the gender column exists in the data template.

Additional Template Defaults

masterUsers.json includes additional default fields which are not populated from Excel:

userPassword
isBlocked
timeZone
forms

These values remain as defined in the JSON template.

Designation Actual ID Mapping

masterUsers.userDetails.designation.itemActualID uses the same designation id as itemID.

designationId -> userDetails.designation.itemID
designationId -> userDetails.designation.itemActualID