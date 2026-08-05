# Church AI Sermon Intelligence & Bible Presentation System
## Documentation Set — Part 1: Executive Summary & Product Requirements Document (PRD)

---

## 0. Executive Summary

**Product.** A Windows desktop application that listens to a live sermon, transcribes it locally, detects spoken Bible references, retrieves the exact verse text from a local database, and lets a media operator approve display of that passage on a secondary monitor/projector — without requiring internet connectivity.

**Architectural stance.** Local-first modular monolith, Clean Architecture (Domain / Application / Infrastructure / WPF), MVVM, SQLite for MVP with a defined migration path to PostgreSQL, deterministic rule-based Bible-reference detection as the primary mechanism with a local LLM used only for disambiguation of the residual ambiguous cases, and mandatory human approval before anything reaches the projector.

**Key correction to the original brief (recorded here, not silently applied):** EF Core is retained for all mutable domain data (sessions, transcripts, settings) but the Bible reference repository (immutable, read-heavy, latency-sensitive) uses Dapper/ADO.NET directly — this is documented as ADR-011.

**What follows** is the PRD. Subsequent parts (SRS, Domain Model, Database Design, and onward) will be delivered as separate, independently reviewable documents rather than compressed into this one, per the scoping discussion above.

---

## 1. Product Requirements Document (PRD)

### 1.1 Product Overview

**Name (working):** Church AI Sermon Intelligence & Bible Presentation System (short form: ChurchAI)

**Category:** Windows desktop application, local-first, single-user-per-installation at MVP.

**One-line description:** Listens to a sermon, detects Bible references as they're spoken, and puts the correct verse on the projector after operator approval — automatically and accurately, without the operator typing or searching.

### 1.2 Problem Statement

[Evidence — stated as a design premise by the requester, treated here as the working problem statement rather than independently verified] During a live church service, a media operator must simultaneously: listen to the preacher, recognize when a Bible reference is spoken (which may be stated in full, abbreviated, or spoken out in natural language — e.g., "turn with me to the book of Romans, chapter eight, verse twenty-eight"), locate that passage in a Bible application or physical Bible, select the correct translation, and display it — all while continuing to monitor the service for the next reference, other slide changes, and general AV operation.

This produces four recurring failure modes:
1. **Latency** — the passage appears well after the preacher has moved on.
2. **Missed references** — quickly spoken or secondary references go undisplayed.
3. **Transcription/selection errors** — wrong verse, wrong translation, or typos under time pressure.
4. **Operator cognitive load** — the task competes with other AV responsibilities (music slides, camera switching, sound).

### 1.3 Vision

A media operator should be able to run a sermon session with the confidence that any clearly spoken Bible reference will be detected, correctly resolved against the actual Bible text, and presented to them for a single-click approval — turning a manual search-and-type task into a review-and-confirm task.

### 1.4 Goals (MVP)

| ID | Goal |
|---|---|
| G-01 | Detect Bible references from live speech with high precision on clearly spoken, standard-form references (e.g., "John 3:16"). |
| G-02 | Retrieve exact verse text from a licensed/local Bible database — never from AI generation. |
| G-03 | Keep a human operator in the loop before any passage is displayed publicly. |
| G-04 | Function fully offline for the entire sermon-to-display pipeline. |
| G-05 | Run acceptably on a modest laptop (16GB RAM, no dedicated GPU). |
| G-06 | Be installable and operable by a non-technical church volunteer. |

### 1.5 Non-Goals (MVP)

Explicitly out of scope for the first release — listed to prevent scope creep, not because they're unimportant:

| ID | Non-Goal | Rationale |
|---|---|---|
| NG-01 | Church membership/accounting features | Unrelated to core workflow |
| NG-02 | Multi-church SaaS / cloud accounts | No validated need yet; adds auth, tenancy, and sync complexity |
| NG-03 | Mobile application | Desktop-only workflow at MVP; revisit in Phase 6 |
| NG-04 | OBS/OpenLP/ProPresenter/PowerPoint integration | Presentation output is abstracted for this later; not built now |
| NG-05 | Streaming/social media publishing | Out of workflow scope |
| NG-06 | Multi-operator concurrent editing | Single-operator MVP |
| NG-07 | Cloud AI as default | Local-first is a hard MVP constraint; cloud AI is a future optional provider only |

### 1.6 Target Users / Personas

**Persona 1 — Media Operator ("Tunde"), primary user.**
Volunteer or part-time staff, moderate technical comfort (comfortable with presentation software, not a developer). Operates the AV booth during services. Needs: fast, low-friction approval workflow; clear visual confidence indicators; ability to override or manually search when detection fails or the preacher references something atypically.

**Persona 2 — Administrator ("Pastor/IT volunteer"), secondary user.**
Configures Bible translations, AI/audio settings, manages sermon history. Lower frequency of use than the operator; needs a settings surface, not a full admin console at MVP.

**Persona 3 — Preacher.**
Indirect user — does not interact with the software directly at MVP but is the source of the audio. Relevant to requirements only insofar as detection must handle natural spoken variation (pace, accent, phrasing), explicitly including Nigerian English as called out in the original brief.

### 1.7 User Journeys (MVP)

**Journey A — Happy path, standard reference.**
Operator opens app → selects microphone and sermon session → clicks Start → preacher says "Let's turn to Romans chapter 8 verse 28" → live transcript updates → within a few seconds, a card appears: `Romans 8:28 — [DISPLAY] [IGNORE] [EDIT]` → operator clicks DISPLAY → passage appears on the projector → preacher moves on → operator clicks to clear or the next reference supersedes it.

**Journey B — Ambiguous/natural-language reference.**
Preacher says "the first chapter of Genesis" → deterministic parser flags low confidence → local LLM resolves to `Genesis 1` (whole chapter) → operator sees the resolved reference with a visibly lower-confidence indicator before approving.

**Journey C — Detection failure / manual fallback.**
Preacher references a passage the system doesn't catch (background noise, overlapping speech, obscure phrasing) → operator manually searches the Bible search screen, selects the verse, and displays it directly — bypassing detection entirely.

**Journey D — Infrastructure failure.**
Local LLM fails to load or times out → system logs the failure, disables AI-assisted resolution for the session, and continues deterministic detection uninterrupted (see Document 14, Error Handling, to be produced separately) → operator is notified via a non-blocking status indicator, not a modal dialog that would interrupt the service.

### 1.8 User Stories (MVP) — representative sample, not exhaustive

| ID | Story | Priority |
|---|---|---|
| US-01 | As an operator, I want to select my microphone from a list so that I capture the correct audio source. | Must |
| US-02 | As an operator, I want to see a live transcript so I can visually confirm what the system is hearing. | Must |
| US-03 | As an operator, I want detected references to appear as reviewable cards, not auto-display, so I retain control over what the congregation sees. | Must |
| US-04 | As an operator, I want to manually search and display any verse, independent of detection, so I'm never blocked by a missed reference. | Must |
| US-05 | As an operator, I want to select the Bible translation used for lookups so it matches what the preacher is using. | Must |
| US-06 | As an administrator, I want to configure which AI models are used so I can tune performance for my hardware. | Should |
| US-07 | As an operator, I want to see whether a detected reference came from deterministic parsing or AI resolution, so I can judge how much to trust it before approving. | Should |
| US-08 | As an operator, I want the session's transcript and detected references saved, so I can review after the service. | Should |
| US-09 | As an administrator, I want the app to keep working if the AI model fails to load, so a single component failure doesn't take down the whole service. | Must |
| US-10 | As an operator, I want to pick which physical monitor is the projector output, so I don't have to guess or rely on Windows display settings. | Must |

### 1.9 Functional Requirements — MVP summary

Full IDs and detail belong in the SRS (Part 2). Summarized categories here for PRD-level completeness:

- Audio device enumeration and selection
- Live audio capture with start/stop/pause
- Local speech-to-text with rolling transcript display
- Deterministic Bible-reference detection (regex/rule-based) as primary path
- AI-assisted resolution for ambiguous/natural-language references as fallback path
- Bible database lookup against a licensed local translation dataset
- Confidence scoring and display of detection source (deterministic vs AI-assisted)
- Operator approval workflow (Display / Ignore / Edit)
- Presenter window with fullscreen output to a selected secondary display
- Manual Bible search independent of the detection pipeline
- Session save/history with transcript and detected-reference log
- Settings for microphone, translation, AI provider/model, display selection

### 1.10 Non-Functional Requirements — MVP summary

- **Offline operation:** the full sermon→display pipeline must not require network access.
- **Latency:** end-to-end detection-to-card-appearance should be low enough to remain useful mid-sermon (specific numeric targets defined in Document 16, Performance Engineering).
- **Resource footprint:** must run acceptably on 16GB RAM / no dedicated GPU.
- **Reliability:** partial failure (AI, audio device, display) must degrade gracefully rather than crash the session.
- **Installability:** installable by a non-technical operator via a standard Windows installer.
- **Data privacy:** audio, transcript, and sermon data remain local unless the user explicitly enables a future sync feature (out of MVP scope).
- **Testability:** business logic (detection, normalization, confidence scoring) must be unit-testable independent of WPF, audio hardware, or a specific AI backend.

### 1.11 MVP Definition (single statement)

MVP = a Windows desktop app that captures live microphone audio, transcribes it locally, detects Bible references spoken in the specified example forms, resolves them against a local Bible database, requires operator approval, and displays the approved passage full-screen on a selected secondary monitor — all offline, all persisted locally, with manual search as a guaranteed fallback.

### 1.12 Future Roadmap (pointer)

Full roadmap is Document 18 (separate deliverable). At PRD level: Phase 2 adds deeper AI-assisted resolution and confidence tuning; Phase 3 hardens for production (installer, diagnostics, auto-update); Phase 4 adds sermon history/search features; Phase 5 adds third-party presentation integrations (OBS/OpenLP/ProPresenter/PowerPoint); Phase 6 — only if commercially justified — introduces an API, cloud sync, and multi-client support.

### 1.13 Success Metrics (MVP)

| ID | Metric | Target (initial, to be validated with real sermon data) |
|---|---|---|
| SM-01 | Detection precision on standard-form references (e.g. "Book C:V") | ≥ 95% |
| SM-02 | Detection recall on standard-form references | ≥ 90% |
| SM-03 | Detection-to-operator-card latency | Target defined in Doc 16; provisional ≤ 4s |
| SM-04 | Sessions completed without a crash | 100% (any crash is a P0 defect) |
| SM-05 | Operator-reported "trust in displayed accuracy" | Qualitative, gathered post-pilot |

[Guess — no field data yet] These are provisional targets pending real-world pilot data; SM-01/SM-02 in particular should be re-baselined once the deterministic parser is tested against actual sermon transcripts, since natural spoken variation (see §1.7 Journey B) will materially affect both.

### 1.14 Risks (PRD-level; full register is Document 25)

| ID | Risk | Impact |
|---|---|---|
| R-01 | Speech recognition accuracy degrades with accent, background noise, or fast preaching | Missed/incorrect references |
| R-02 | Bible translation licensing constraints limit which datasets can ship locally | Legal/product limitation |
| R-03 | Local LLM inference too slow on target hardware for perceived "live" behavior | UX degradation |
| R-04 | Over-reliance on AI resolution erodes the deterministic-first design intent during implementation | Architectural drift, accuracy risk |

### 1.15 Assumptions

- [Assumption] The operator has a functioning secondary display/projector connection before the session starts; hot-plug detection is a should-have, not a blocking MVP requirement, unless stated otherwise.
- [Assumption] A licensed or public-domain Bible translation dataset in a locally usable format (e.g., structured text/DB export) is obtainable; licensing research itself is out of scope for this documentation pass and must be resolved before shipping (tracked as an open decision, see final checklist in the closing document).
- [Assumption] Single active sermon session at a time; no concurrent multi-session support at MVP.

### 1.16 Constraints

- Windows-only at MVP (per stated technology constraints).
- No mandatory internet dependency for core workflow.
- Target hardware floor: 16GB RAM, no dedicated GPU (Intel Iris Xe class integrated graphics).

### 1.17 Acceptance Criteria (PRD-level)

The MVP is acceptable for internal pilot when: (1) all Must-priority user stories in §1.8 are implemented and pass their corresponding tests (traceability in Document 26); (2) a full end-to-end session — mic in, transcript out, reference detected, verse retrieved, operator-approved, displayed on a real secondary monitor — completes without manual intervention outside the defined approval step; (3) the AI-failure and audio-failure degradation paths (Journey D) are verified to not crash or block the session.
