# TEST_READY — Polishly Windows Companion Automated Test Reference

**Document Version:** 2.0.0
**Last updated:** August 2026
**Target codebase:** `windows/` (`Polishly.Windows.sln`)

> **Status of record:** see
> [`docs/WINDOWS-IMPLEMENTATION-STATUS.md`](docs/WINDOWS-IMPLEMENTATION-STATUS.md)
> for the authoritative phase-by-phase status against
> [`docs/WINDOWS-PLAN.md`](docs/WINDOWS-PLAN.md)'s Definition of Done. This
> document is a command reference for the automated test assemblies only —
> it is not a readiness declaration. Passing these tests is necessary but
> not sufficient: headless tests are never counted as real application
> compatibility results, and the interactive qualification work listed in
> the status doc (multi-DPI matrix, 20-attempt Notepad/Teams capture runs,
> live provider smoke tests, signed release artifacts, private beta) is
> tracked there, not here.

---

## 1. What these tests cover

`windows/tests/` holds three assemblies exercising the cross-platform logic
(state machine, diff engine, prompt builder, capability rules, provider
clients) and Windows-specific integration code (clipboard transaction,
credential storage, UI Automation capture, popup positioning) under a
hand-rolled xunit-compatible runner. As of the last CI run recorded in
`docs/WINDOWS-IMPLEMENTATION-STATUS.md`, the current counts are 93 core
tests, 159 platform tests, and 35 app-compatibility tests (287 total) — see
that document for the CI run link and current numbers, since this file is
not kept in sync automatically.

## 2. Test runner invocation commands

The test projects set `IsTestProject=false` and `OutputType=Exe`, so they
are **custom runners, not xunit test projects** — `dotnet test` will not
discover or run them.

```bash
# Build first
dotnet build windows/Polishly.Windows.sln --configuration Release -warnaserror

# Run each assembly directly — not `dotnet test`
dotnet run --project windows/tests/Polishly.Core.Tests/Polishly.Core.Tests.csproj --configuration Release --no-build
dotnet run --project windows/tests/Polishly.Platform.Tests/Polishly.Platform.Tests.csproj --configuration Release --no-build
dotnet run --project windows/tests/Polishly.AppCompatibility.Tests/Polishly.AppCompatibility.Tests.csproj --configuration Release --no-build
```

On a non-Windows machine, the solution falls back to a `net7.0`/`net10.0`
cross-platform configuration with `HAS_WPF` undefined — WPF windows, XAML,
and native Win32/UIA code paths are excluded from that build entirely, so a
passing local run off Windows does not exercise them. The
[`windows-companion` GitHub Actions workflow](../.github/workflows/windows-companion.yml)
runs on `windows-latest` with the real `net10.0-windows` / WPF configuration
and is the only build that compiles and tests the actual shipped code path.

## 3. Feature-to-test mapping

| Feature subsystem | Representative test |
|---|---|
| UI Automation capture | `CaptureSelectionAsync_ProducesValidSelectionContext` |
| Guarded clipboard transaction | `ExecuteSafePasteAsync_MatchingSequenceNumber_SucceedsWithoutFallback` |
| Clipboard sequence-mismatch fallback | `ExecuteSafePasteAsync_SequenceMismatch_AbortsPasteAndTriggersCopyFallback` |
| Password-field protection | `SensitiveFieldBlockAndFallbackToCopy_PasswordManager` |
| Elevated-process detection | `SensitiveFieldBlockAndFallbackToCopy_ElevatedAdminProcess` |
| Per-app capability profiles | `AppCapabilityRulesTests.GetProfile_KnownApps_ReturnsConfiguredProfile` |
| Global hotkey registration | `HotkeyListener_Register_ReturnsBoolStatus` |
| Rewrite state machine | `RewriteStateMachineTests.FullLifecycle_HappyPath_TransitionsCorrectly` |
| Prompt builder | `PromptBuilderTests.BuildSystemPrompt_AllModes_ContainsExpectedKeywords` |
| Word-level diff engine | `WordDiffEngineTests.ComputeDiff_SingleWordChange_ReturnsCorrectSegments` |
| Provider streaming (OpenAI/Anthropic/Groq/Cerebras/Demo) | `ProviderStreamingTests.OpenAiProvider_StreamRewriteAsync_ParsesSseTokensSuccessfully` |
| Credential Manager storage | `CredentialManagerFeatureTests.SaveAndGetApiKeyAsync_Roundtrip_ReturnsStoredKey` |
| Redacted diagnostic logging | `RedactedLoggerTests.RedactedLogger_ZeroKeyLeaks_AcrossAllLogTypes` |
| Popup positioning | `PopupPositionerTests.PopupPositioner_SmartFlipAbove_WhenOverflowingWorkAreaBottom` |
| Onboarding flow | `OnboardingViewModelTests.OnboardingViewModel_NavigationForward_CyclesThrough6Steps` |

This table names representative tests per subsystem, not an exhaustive list,
and does not by itself constitute a compatibility or readiness claim for
any specific application — see `docs/WINDOWS-IMPLEMENTATION-STATUS.md`.
