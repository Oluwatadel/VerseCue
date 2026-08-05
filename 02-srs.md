# Church AI Sermon Intelligence & Bible Presentation System
## Documentation Set — Part 2: Software Requirements Specification (SRS)

Conformant in structure to IEEE 830 / ISO/IEC/IEEE 29148 practice, adapted for a desktop product.

---

## 1. Introduction

### 1.1 Purpose
This SRS defines the functional and non-functional requirements for the MVP release of ChurchAI, a local-first Windows desktop application. It is the authoritative requirements source for the Use Case Specification, Domain Model, and downstream design documents.

### 1.2 Scope
Covers the MVP feature set defined in PRD §1.11: audio capture, local speech-to-text, Bible reference detection/resolution, Bible database lookup, operator approval, presentation output, manual search, settings, session history, and offline operation. Excludes items in PRD §1.5 (Non-Goals).

### 1.3 Definitions

| Term | Definition |
|---|---|
| Reference | A spoken or written pointer to a Bible passage (e.g., "Romans 8:28") |
| Detected Reference | A reference identified by the system from a transcript, prior to validation |
| Resolved Reference | A detected reference successfully normalized to a canonical Book/Chapter/Verse(s) form |
| Deterministic Detection | Rule/regex-based parsing, no AI inference involved |
| AI-Assisted Resolution | Local LLM used to interpret ambiguous or natural-language phrasing |
| Operator | The person running the AV booth during the service (primary MVP user) |
| Presenter Window | The output window rendered fullscreen on the secondary display/projector |
| Session | One continuous sermon-monitoring run, from Start to Stop |

### 1.4 System Overview
ChurchAI is a single-user Windows desktop application following Clean Architecture with an MVVM presentation layer. It captures audio locally, transcribes it with a local speech-to-text engine, runs a two-stage (deterministic-then-AI) Bible reference detection pipeline, looks up canonical verse text in a local SQLite-backed Bible database, and renders operator-approved passages to a second display. All processing for this pipeline occurs on-device.

### 1.5 User Classes

| Class | Description | Frequency |
|---|---|---|
| Media Operator | Primary hands-on user during live services | Every service |
| Administrator | Configures settings, translations, AI/audio providers | Occasional |

(Preacher is a non-interacting actor — audio source only; see Use Case doc.)

---

## 2. Functional Requirements

IDs follow the pattern `FR-<AREA>-###`.

### 2.1 Audio (FR-AUDIO)

| ID | Requirement |
|---|---|
| FR-AUDIO-001 | The system shall enumerate available audio input devices at startup and on demand. |
| FR-AUDIO-002 | The system shall allow the operator to select an active microphone from the enumerated list. |
| FR-AUDIO-003 | The system shall capture audio continuously once a session is started, until stopped or paused. |
| FR-AUDIO-004 | The system shall detect microphone disconnection during an active session and surface a non-blocking status indicator. |
| FR-AUDIO-005 | The system shall allow pausing and resuming audio capture within a session without ending the session. |

### 2.2 Speech-to-Text (FR-STT)

| ID | Requirement |
|---|---|
| FR-STT-001 | The system shall transcribe captured audio to text using a local speech-to-text engine, without sending audio off-device. |
| FR-STT-002 | The system shall display a rolling live transcript in the UI as text becomes available. |
| FR-STT-003 | The system shall timestamp transcript segments for later reference and session history. |
| FR-STT-004 | The system shall continue operating (detection disabled or degraded, not crashed) if the STT engine becomes unavailable mid-session, per FR-ERR-002. |

### 2.3 Bible Reference Detection (FR-BIBLE)

| ID | Requirement |
|---|---|
| FR-BIBLE-001 | The system shall apply deterministic rule/regex-based detection to each transcript segment as the primary detection mechanism. |
| FR-BIBLE-002 | The system shall recognize standard-form references (e.g., "John 3:16", "Romans 8:28-30", "1 Corinthians 13:4-7", "Psalm 23"). |
| FR-BIBLE-003 | The system shall recognize spoken-number and natural-language forms (e.g., "John chapter three verse sixteen", "the first chapter of Genesis") via the deterministic layer where patterns allow, falling back to AI-assisted resolution otherwise. |
| FR-BIBLE-004 | The system shall assign a confidence score to each detected reference. |
| FR-BIBLE-005 | The system shall invoke AI-assisted resolution only when deterministic confidence falls below a configurable threshold. |
| FR-BIBLE-006 | The system shall validate every resolved reference against the local Bible database before presenting it to the operator; unvalidated references shall not be presented as displayable. |
| FR-BIBLE-007 | The system shall support detection of multiple distinct references within a single transcript segment. |
| FR-BIBLE-008 | The system shall record, for every detected reference, whether it was resolved deterministically or via AI assistance, for operator visibility (US-07). |

### 2.4 Bible Database & Lookup (FR-DB)

| ID | Requirement |
|---|---|
| FR-DB-001 | The system shall store at least one Bible translation locally in a queryable form. |
| FR-DB-002 | The system shall retrieve exact verse text for a resolved reference from the local database; the system shall never use AI-generated text as displayable Bible content. |
| FR-DB-003 | The system shall support multiple installed translations and allow the operator/administrator to select the active translation. |
| FR-DB-004 | The system shall support manual free-text and structured Bible search independent of the detection pipeline (US-04). |

### 2.5 Operator Approval (FR-APPROVAL)

| ID | Requirement |
|---|---|
| FR-APPROVAL-001 | The system shall present each resolved, validated reference to the operator as a reviewable card with Display / Ignore / Edit actions. |
| FR-APPROVAL-002 | The system shall not send any passage to the presenter window without explicit operator approval, except where the operator has manually initiated a display action directly (FR-DB-004 path). |
| FR-APPROVAL-003 | The system shall allow the operator to edit a resolved reference (e.g., correct chapter/verse) before approving display. |

### 2.6 Presentation (FR-PRESENT)

| ID | Requirement |
|---|---|
| FR-PRESENT-001 | The system shall enumerate connected displays and allow the operator to designate one as the presenter output. |
| FR-PRESENT-002 | The system shall render the approved passage fullscreen on the designated display. |
| FR-PRESENT-003 | The system shall allow the operator to clear/hide the presenter output on demand. |
| FR-PRESENT-004 | The system shall allow basic display configuration (font size, theme/background) at MVP. |
| FR-PRESENT-005 | The system shall detect loss of the designated secondary display during a session and surface a non-blocking status indicator without crashing. |

### 2.7 Session & History (FR-SESSION)

| ID | Requirement |
|---|---|
| FR-SESSION-001 | The system shall persist each sermon session, including transcript and detected-reference log, locally. |
| FR-SESSION-002 | The system shall allow the operator/administrator to browse and view past session history. |

### 2.8 Settings & Configuration (FR-SETTINGS)

| ID | Requirement |
|---|---|
| FR-SETTINGS-001 | The system shall allow configuration of the active AI provider/model. |
| FR-SETTINGS-002 | The system shall allow configuration of the active Bible translation. |
| FR-SETTINGS-003 | The system shall allow configuration of the audio input device. |
| FR-SETTINGS-004 | The system shall allow configuration of the AI-assistance confidence threshold (FR-BIBLE-005). |

---

## 3. Non-Functional Requirements

IDs follow `NFR-<AREA>-###`.

### 3.1 Performance (NFR-PERF)
| ID | Requirement |
|---|---|
| NFR-PERF-001 | End-to-end latency from spoken reference to operator-visible card shall meet the target defined in Document 16 (Performance Engineering) on the reference hardware profile (16GB RAM, 11th-gen i7, integrated graphics). |
| NFR-PERF-002 | Manual Bible search shall return results in under 1 second on the reference hardware. |

### 3.2 Reliability (NFR-REL)
| ID | Requirement |
|---|---|
| NFR-REL-001 | Failure of the AI-assisted resolution component shall not prevent deterministic detection from continuing (ADR-007). |
| NFR-REL-002 | Failure of the STT engine, audio device, or secondary display shall degrade the relevant feature without crashing the application. |
| NFR-REL-003 | No single component failure shall cause loss of already-captured transcript/session data. |

### 3.3 Security & Privacy (NFR-SEC, NFR-PRIV)
| ID | Requirement |
|---|---|
| NFR-SEC-001 | AI output shall never be used to execute operating-system commands or arbitrary code. |
| NFR-SEC-002 | All AI-provider credentials (for any future non-local provider) shall be stored using OS-level secure storage, never in plaintext config. |
| NFR-PRIV-001 | Audio, transcript, and session data shall remain on the local device unless the user explicitly enables a future sync feature (out of MVP scope). |

### 3.4 Offline Operation (NFR-OFFLINE)
| ID | Requirement |
|---|---|
| NFR-OFFLINE-001 | The full pipeline (audio → transcript → detection → lookup → display) shall function with no network connection present. |

### 3.5 Maintainability & Testability (NFR-MAINT)
| ID | Requirement |
|---|---|
| NFR-MAINT-001 | Business logic (detection, normalization, confidence scoring, approval workflow) shall reside outside WPF code-behind and be unit-testable without a UI, real audio hardware, or a live AI model. |
| NFR-MAINT-002 | Audio, STT, AI, and Bible-repository components shall be accessed only through interfaces defined in the Application layer, permitting substitution without changes to business logic. |

### 3.6 Compatibility & Deployment (NFR-COMPAT, NFR-DEPLOY)
| ID | Requirement |
|---|---|
| NFR-COMPAT-001 | The system shall run on a supported Windows 10/11 64-bit target with the specified reference hardware floor. |
| NFR-DEPLOY-001 | The system shall be installable via a standard Windows installer without requiring the operator to manually install a separate runtime, provided the installer bundles or provisions the required .NET runtime and model files. |

### 3.7 Logging (NFR-LOG)
| ID | Requirement |
|---|---|
| NFR-LOG-001 | The system shall log errors and key pipeline events (session start/stop, detection failures, AI/audio/display failures) locally for diagnostics. |
| NFR-LOG-002 | Logs shall not contain AI-provider credentials or other secrets. |

---

## 4. System, Hardware, and Software Interfaces (summary)

| Interface | Description |
|---|---|
| Microphone (hardware) | Standard Windows audio input device via WASAPI/NAudio abstraction |
| Secondary display (hardware) | Standard Windows multi-monitor output |
| Local STT engine (software) | Whisper-family model invoked in-process or via local binding (see AI Architecture doc) |
| Local LLM (software) | GGUF-format model invoked via local inference runtime |
| Bible database (software) | Local SQLite-backed store (Bible reference data) |
| Session store (software) | Local SQLite-backed store (mutable session/settings data) |

Detailed interface contracts are specified in Document 11 (Service/API Contracts), not duplicated here.

---

## 5. Acceptance Criteria (SRS-level)

Each functional requirement above is considered met when its corresponding test case(s) in the Traceability Matrix (Document 26) pass, and the requirement's behavior is demonstrable in an end-to-end session per PRD §1.17.

---

*Note on completeness:* Sections 9–27 as originally enumerated in the request (Hardware Interfaces detail, Software Interfaces detail, Audio/AI/Database/Presentation requirement deep-dives, Offline/Security/Privacy elaboration, Error Handling, Logging elaboration, Performance/Reliability/Maintainability/Scalability/Compatibility elaboration, Deployment detail, Acceptance Criteria detail) are intentionally addressed at the requirement-table level above and will be elaborated further in their dedicated documents (Error Handling = Doc 14, Performance = Doc 16, Deployment = Doc 20, Security & Privacy = Doc 15) rather than duplicated at length here, to avoid redundant, unverifiable restatement across documents.
