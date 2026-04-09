# Requirements Document

## Introduction

This feature adds AI-based sensitive data detection to the DataDesensitization Blazor application. A pre-trained ONNX NER/classification model, loaded via ML.NET, classifies column names and schema metadata (table names, data types) as potentially sensitive. The AI detector runs fully locally with no external API calls and only processes schema metadata — never actual row data. It integrates into the existing auto-detect pipeline alongside the current regex-based detection in `RuleConfigurationService.AutoDetectRules()`.

## Glossary

- **AI_Detector**: The service that loads a pre-trained ONNX model via ML.NET and classifies schema metadata as sensitive or non-sensitive.
- **Schema_Metadata**: Column names, data types, nullability, max length, foreign key status, table names, and schema names — no actual row data.
- **ONNX_Model**: A pre-trained Open Neural Network Exchange model file used for NER or text classification, stored locally in the application.
- **Sensitivity_Label**: A classification label produced by the AI_Detector indicating the type of sensitive data (e.g., PII_Name, PII_Email, PII_Address, Financial, Credential, Medical, None).
- **Confidence_Score**: A floating-point value between 0.0 and 1.0 representing the AI_Detector's certainty in a Sensitivity_Label prediction.
- **Confidence_Threshold**: A configurable minimum Confidence_Score (default 0.7) below which predictions are discarded.
- **Detection_Result**: A record containing the table name, column name, Sensitivity_Label, Confidence_Score, and suggested DesensitizationStrategyType.
- **Auto_Detect_Pipeline**: The combined detection flow that merges results from the regex-based detector and the AI_Detector, invoked by `AutoDetectRules()`.
- **Rule_Configuration_Service**: The existing `RuleConfigurationService` that manages desensitization rules and runs auto-detection.
- **Model_Provider**: The service responsible for loading, caching, and providing the ONNX inference session to the AI_Detector.

## Requirements

### Requirement 1: ONNX Model Loading and Lifecycle

**User Story:** As a developer, I want the application to load a pre-trained ONNX model at startup and cache the inference session, so that AI-based detection is available without repeated model loading overhead.

#### Acceptance Criteria

1. WHEN the application starts, THE Model_Provider SHALL load the ONNX_Model file from a configurable local file path into an ML.NET inference session.
2. THE Model_Provider SHALL cache the loaded inference session for the lifetime of the application to avoid repeated loading.
3. IF the ONNX_Model file is missing or corrupt, THEN THE Model_Provider SHALL log a descriptive error and allow the application to continue operating with regex-only detection.
4. THE Model_Provider SHALL execute all model operations locally without making external network calls.

### Requirement 2: Schema Metadata Classification

**User Story:** As a developer, I want the AI detector to classify column names and schema metadata as sensitive or non-sensitive, so that the system can detect sensitive columns that regex patterns miss.

#### Acceptance Criteria

1. WHEN the AI_Detector receives Schema_Metadata for a column, THE AI_Detector SHALL produce a Sensitivity_Label and a Confidence_Score for that column.
2. THE AI_Detector SHALL accept only Schema_Metadata (column name, data type, table name, schema name, nullability, max length, foreign key status) as input — never actual row data.
3. THE AI_Detector SHALL tokenize column names by splitting on underscores, camelCase boundaries, and common abbreviations before feeding tokens to the ONNX_Model.
4. WHEN the AI_Detector classifies a column, THE AI_Detector SHALL include the table name as contextual input to improve classification accuracy.
5. THE AI_Detector SHALL map each Sensitivity_Label to a default DesensitizationStrategyType (e.g., PII_Name → Randomization, PII_Email → Masking, Financial → Masking, Credential → Nullification).

### Requirement 3: Confidence Threshold Filtering

**User Story:** As a user, I want to configure a minimum confidence threshold for AI predictions, so that only sufficiently confident detections are surfaced as suggestions.

#### Acceptance Criteria

1. THE AI_Detector SHALL discard predictions with a Confidence_Score below the Confidence_Threshold.
2. THE Confidence_Threshold SHALL default to 0.7 and be configurable via `appsettings.json`.
3. WHEN the Confidence_Threshold is set to a value outside the range 0.0 to 1.0, THE AI_Detector SHALL clamp the value to the nearest valid bound (0.0 or 1.0).

### Requirement 4: Integration with Auto-Detect Pipeline

**User Story:** As a user, I want AI-based detection to run alongside regex-based detection when I click "Auto-Detect Sensitive Columns", so that I get a combined set of suggestions from both methods.

#### Acceptance Criteria

1. WHEN `AutoDetectRules()` is invoked, THE Auto_Detect_Pipeline SHALL run both the regex-based detector and the AI_Detector on the provided Schema_Metadata.
2. WHEN both detectors identify the same column, THE Auto_Detect_Pipeline SHALL prefer the regex-based result (higher specificity) and discard the duplicate AI result.
3. THE Auto_Detect_Pipeline SHALL merge results from both detectors into a single list of DesensitizationRule suggestions.
4. THE Auto_Detect_Pipeline SHALL skip foreign key columns before passing Schema_Metadata to the AI_Detector.
5. IF the ONNX_Model is unavailable, THEN THE Auto_Detect_Pipeline SHALL fall back to regex-only detection without error.

### Requirement 5: Detection Result Metadata

**User Story:** As a user, I want to see which detection method (regex or AI) produced each suggestion and the AI confidence score, so that I can make informed decisions about accepting or dismissing suggestions.

#### Acceptance Criteria

1. THE Detection_Result SHALL include a source indicator specifying whether the detection came from the regex detector or the AI_Detector.
2. WHEN the AI_Detector produces a Detection_Result, THE Detection_Result SHALL include the Confidence_Score and the Sensitivity_Label.
3. THE Rule_Configuration_Service SHALL return Detection_Results that preserve the source and confidence metadata alongside the suggested DesensitizationRule.

### Requirement 6: UI Display of AI Detection Results

**User Story:** As a user, I want the auto-detect suggestions table to show the detection source and confidence score, so that I can distinguish AI-based suggestions from regex-based ones.

#### Acceptance Criteria

1. WHEN auto-detect suggestions are displayed, THE RuleConfiguration page SHALL show a badge indicating the detection source ("Regex" or "AI") for each suggestion.
2. WHEN an AI-based suggestion is displayed, THE RuleConfiguration page SHALL show the Confidence_Score as a percentage next to the suggestion.
3. WHEN an AI-based suggestion is displayed, THE RuleConfiguration page SHALL show the Sensitivity_Label assigned by the AI_Detector.

### Requirement 7: Configuration via appsettings.json

**User Story:** As a developer, I want to configure the ONNX model path and confidence threshold in appsettings.json, so that deployment settings can be adjusted without code changes.

#### Acceptance Criteria

1. THE application SHALL read the ONNX_Model file path from the `AiDetection:ModelPath` key in `appsettings.json`.
2. THE application SHALL read the Confidence_Threshold from the `AiDetection:ConfidenceThreshold` key in `appsettings.json`.
3. IF the `AiDetection` configuration section is missing, THEN THE application SHALL disable AI-based detection and use regex-only detection.

### Requirement 8: Column Name Tokenizer

**User Story:** As a developer, I want a tokenizer that splits column names into meaningful tokens, so that the ONNX model receives well-structured input for classification.

#### Acceptance Criteria

1. THE AI_Detector SHALL split column names on underscore characters (e.g., `first_name` → `["first", "name"]`).
2. THE AI_Detector SHALL split column names on camelCase boundaries (e.g., `firstName` → `["first", "Name"]`).
3. THE AI_Detector SHALL normalize all tokens to lowercase before passing them to the ONNX_Model.
4. THE AI_Detector SHALL expand common abbreviations (e.g., `addr` → `address`, `dob` → `date of birth`, `ssn` → `social security number`, `pwd` → `password`, `cc` → `credit card`).
5. FOR ALL column name strings, tokenizing then joining with spaces SHALL produce a deterministic, reproducible result (round-trip consistency).
