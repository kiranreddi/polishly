# Polishly Windows — Test Infrastructure Specification

**Document Status:** Final / Production Ready  
**Last Updated:** July 2026  
**Target Solution:** `windows/Polishly.Windows.sln`  
**Framework Version:** .NET 7.0 / .NET 10.0-windows  

---

## 1. Executive Summary

This document specifies the test infrastructure, test architecture, testing methodology, and coverage guidelines for the Polishly for Windows companion application. Polishly is designed around a zero-trust text injection guarantee: *Never paste unless Polishly can prove it is returning to the exact source application and field.*

To guarantee reliability, performance, and security across diverse Windows desktop, web, and Electron applications, the test suite implements a **4-Tier Testing Model** spanning unit logic, boundary conditions, cross-feature integration, and real-world application workloads.

---

## 2. Feature Inventory

The test infrastructure covers all core components of Polishly for Windows:

| Subsystem | Components Covered | Core Invariants & Protections Verified |
|---|---|---|
| **Capture Engine** | `UIAutomationCapture`, `WindowTracker` | Accurate selection extraction, selection bounds calculation, fallback handling when UIA is unsupported. |
| **Clipboard Safety** | `GuardedClipboardTransaction` | Clipboard format snapshotting, sequence number verification (`GetClipboardSequenceNumber`), atomic paste, original clipboard restoration, copy fallback. |
| **Sensitive Field Detector** | `SensitiveFieldDetector` | Detection of password fields (`IsPassword=true`), password managers (`1password`, `keepassxc`, `bitwarden`), CLI terminals (`cmd`, `powershell`), and elevated UAC admin processes. |
| **App Capability System** | `AppCapabilityRules`, `AppProfile` | Application classification (Notepad, Teams, Outlook, Word, Slack, Chrome, Edge, VS Code, OneNote), enforcing capability profiles (UIA vs Guarded Clipboard). |
| **Rewrite Engine** | `RewriteStateMachine`, `PromptBuilder`, `WordDiffEngine` | State transitions (`Idle` -> `Capturing` -> `Streaming` -> `Diffing` -> `Accepted`), mode-specific system prompt generation, word-level diff calculation with O(N) prefix/suffix optimization. |
| **AI Providers** | `DemoProvider`, `OpenAiProvider`, `AnthropicProvider`, `GroqProvider`, `CerebrasProvider` | Streaming token generation (`IAsyncEnumerable<RewriteToken>`), API key validation, HTTP error status code translation (401, 429, 503), cancellation token honor. |
| **Platform Integration** | `CredentialManager`, `GlobalHotkeyListener`, `RedactedLogger` | Windows Credential Manager storage (`CredWrite`/`CredRead`), hotkey registration (`RegisterHotKey`/`WM_HOTKEY`), diagnostic log redaction excluding credentials and user text. |
| **UI ViewModels** | `PopupViewModel`, `SettingsViewModel`, `OnboardingViewModel`, `PopupPositioner` | Diff rendering, non-activating window placement (`WS_EX_NOACTIVATE`), theme synchronization, onboarding 6-step flow, blocklist management. |

---

## 3. Test Architecture (4-Tier Model)

The test suite is structured into four distinct testing tiers located under `windows/tests/`:

```text
windows/tests/
├── Polishly.Core.Tests/               # Tier 1 & Tier 2 Core Engine Tests
│   ├── AppCapabilityRulesTests.cs
│   ├── DiffCalculationFeatureTests.cs
│   ├── PromptBuilderTests.cs
│   ├── PromptBuildingFeatureTests.cs
│   ├── RewriteStateMachineTests.cs
│   ├── StateMachineTests.cs
│   ├── StressAndBoundaryTests.cs
│   ├── Tier2EmptyStringTests.cs
│   ├── Tier2LargePayloadTests.cs
│   ├── Tier2MultilineTextTests.cs
│   ├── Tier2UnicodeEmojiTests.cs
│   └── WordDiffEngineTests.cs
├── Polishly.Platform.Tests/           # Tier 1, Tier 2 & Tier 3 Platform & Integration Tests
│   ├── ClipboardSafetyTests.cs
│   ├── CredentialManagerFeatureTests.cs
│   ├── CredentialManagerTests.cs
│   ├── GuardedClipboardFeatureTests.cs
│   ├── HotkeyFeatureTests.cs
│   ├── NonActivatingWindowFlagsTests.cs
│   ├── OnboardingViewModelTests.cs
│   ├── PopupPositionerTests.cs
│   ├── PopupViewModelTests.cs
│   ├── ProviderConfigFeatureTests.cs
│   ├── ProviderStreamingTests.cs
│   ├── RedactedLoggerTests.cs
│   ├── SensitiveFieldDetectorTests.cs
│   ├── SensitiveFieldFeatureTests.cs
│   ├── SettingsFeatureTests.cs
│   ├── SettingsViewModelTests.cs
│   ├── Tier2AuthCredentialErrorTests.cs
│   ├── Tier2ElevatedProcessTests.cs
│   ├── Tier2NetworkTransientErrorTests.cs
│   ├── Tier2SequenceMismatchTests.cs
│   └── Tier3CrossFeatureTests.cs
└── Polishly.AppCompatibility.Tests/   # Tier 4 Real-World Application Workload Tests
    ├── CaptureWorkloadTests.cs
    ├── CompatibilityProfilesTests.cs
    ├── SelectionCaptureTests.cs
    └── Tier4RealWorldWorkloadTests.cs
```

### Tier 1 — Unit & State Machine Tests
- **Objective**: Validate pure functional units, state machines, prompt building, and diff calculation in isolation.
- **Coverage**: `RewriteStateMachine`, `PromptBuilder`, `WordDiffEngine`, `AppSettings`, `OnboardingViewModel`, `PopupPositioner`.

### Tier 2 — Boundary, Negative & Stress Tests
- **Objective**: Verify robustness against unexpected or extreme inputs and system states.
- **Coverage**:
  - **Empty/Whitespace Inputs**: Null, empty, or whitespace-only selection contexts.
  - **Large Payloads**: 100,000+ character payloads tested for sub-second diff computation and zero memory leaks.
  - **Multiline & Unicode**: CRLF/LF line normalization, RTL text (Arabic/Hebrew), complex emoji surrogate pairs.
  - **Transient Errors**: Network timeouts, HTTP 429 rate limits, HTTP 503 unavailability, cancellation token aborts.
  - **Security Boundaries**: Elevated process blocking, password manager vault fields, clipboard sequence number mismatches.

### Tier 3 — Cross-Feature Combination Tests
- **Objective**: Verify interactions between independent modules operating in tandem.
- **Scenarios**:
  - Global Hotkey + UI Automation Capture
  - Global Hotkey + Guarded Clipboard Fallback
  - Selection Capture + Provider Streaming + Word Diff + Popup ViewModel
  - Guarded Clipboard + Sequence Number Mismatch
  - Sensitive Field Detector + Guarded Clipboard (Password Manager & Elevated Admin)
  - App Settings + System Tray + Theme Service Synchronization
  - App Settings + Hotkey Re-registration
  - Provider Selection + Word Diff Engine
  - State Machine + Guarded Clipboard Transaction
  - Capability Rules + Sensitive Field Detector Policy
  - Capability Rules + Guarded Clipboard Fallback Enforcement
  - Credential Manager + Provider Streaming Key Retrieval
  - Redacted Logger + Sensitive Field Detector

### Tier 4 — Real-World Application Workload Scenarios
- **Objective**: Validate end-to-end user workflows against realistic target application profiles.
- **Scenarios**:
  1. **Notepad Workload**: Native Win32 text editor with direct UI Automation capture, streaming rewrite, and replacement.
  2. **Microsoft Teams Workload**: Electron application requiring guarded clipboard fallback with sequence verification and clipboard restoration.
  3. **Outlook Classic Workload**: Office rich text email composition, prompt building for professional email improvement, popup stream, and guarded paste.
  4. **Slack Workload**: Electron chat channel draft input, verifying clipboard sequence integrity.
  5. **VS Code Workload**: Monaco code editor comment rewrite, multi-line formatting preservation, and diff generation.
  6. **Chrome Gmail Workload**: Chromium web browser text field compatibility, non-activating popup positioning.
  7. **Elevated Admin Prompt Workload**: Command Prompt / PowerShell elevated process detection, blocking in-place paste, falling back to Copy.
  8. **Password Manager Vault Workload**: 1Password / KeePass master password field detection, blocking text capture and paste.

---

## 4. Quality & Coverage Thresholds

All test runs must satisfy the following strict quality gates:

| Quality Gate | Metric / Target | Status |
|---|---|---|
| **Build Warnings** | 0 warnings | PASS (0 warnings) |
| **Build Errors** | 0 errors | PASS (0 errors) |
| **Test Pass Rate** | 100% (259/259 tests) | PASS (259/259 passed) |
| **Wrong-Window Pastes** | 0 occurrences | PASS (Enforced by Safety Guard 1) |
| **Clipboard Corruption** | 0 occurrences | PASS (Enforced by Safety Guard 4) |
| **Credential/Text Logging Leaks** | 0 occurrences | PASS (Enforced by `RedactedLogger`) |
| **Large Payload Processing** | < 1.0 second for 100k chars | PASS (O(N) prefix/suffix diff optimization) |

---

## 5. Test Execution Instructions

### Build Solution
```bash
dotnet build windows/Polishly.Windows.sln
```

### Run Full Test Suite
```bash
dotnet test windows/Polishly.Windows.sln
```

### Run Tier Test Assemblies Directly
```bash
# Tier 1 & 2 Core Engine Tests (93 tests)
dotnet run --project windows/tests/Polishly.Core.Tests/Polishly.Core.Tests.csproj

# Tier 1, 2 & 3 Platform & Cross-Feature Tests (142 tests)
dotnet run --project windows/tests/Polishly.Platform.Tests/Polishly.Platform.Tests.csproj

# Tier 4 Real-World Application Workload Tests (24 tests)
dotnet run --project windows/tests/Polishly.AppCompatibility.Tests/Polishly.AppCompatibility.Tests.csproj
```
