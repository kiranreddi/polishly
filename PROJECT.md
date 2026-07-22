# Project: Polishly Windows Companion Application

## Architecture
```text
windows/
├── Polishly.Windows.sln
├── src/
│   ├── Polishly.App/
│   │   ├── Views/ (Popup, Settings, Onboarding)
│   │   ├── ViewModels/
│   │   ├── Services/ (Tray, Navigation, Theme)
│   │   └── App.xaml
│   ├── Polishly.Core/
│   │   ├── StateMachine/ (RewriteStateMachine, States)
│   │   ├── Prompts/ (PromptBuilder, Fixatures)
│   │   ├── Diff/ (WordDiffEngine, DiffSegment)
│   │   └── Capabilities/ (AppCapabilityRules, AppProfile)
│   ├── Polishly.WindowsIntegration/
│   │   ├── Capture/ (UIAutomationCapture, WindowTracker)
│   │   ├── Clipboard/ (GuardedClipboardTransaction, FormatSnapshot)
│   │   ├── Injection/ (TextInjector, SendInputHelper)
│   │   ├── Hotkey/ (GlobalHotkeyListener)
│   │   ├── Security/ (CredentialManager, ElevationDetector, SensitiveFieldDetector)
│   │   └── Native/ (Win32Native, User32, Kernel32, Advapi32)
│   └── Polishly.Providers/
│       ├── OpenAI/ (OpenAiProvider)
│       ├── Anthropic/ (AnthropicProvider)
│       ├── Groq/ (GroqProvider)
│       ├── Cerebras/ (CerebrasProvider)
│       ├── Demo/ (DemoProvider)
│       └── Abstractions/ (IAiProvider, ProviderConfig, StreamToken)
└── tests/
    ├── Polishly.Core.Tests/
    ├── Polishly.Platform.Tests/
    └── Polishly.AppCompatibility.Tests/
```

## Milestones
| # | Name | Scope | Dependencies | Status | Conversation ID |
|---|------|-------|-------------|--------|-----------------|
| M0 | E2E Test Suite Track | Opaque-box E2E test infra & Tiers 1-4 tests (TEST_READY.md) | None | DONE | 4296604b-e9f4-4fa0-bc9b-68644c9a863d |
| M1 | Solution & Core Domain Scaffolding | Solution structure, Polishly.Core, state machine, diff, prompts, core tests | None | DONE | f054cd28-5a88-4e46-b36e-b0fe75a88edc |
| M2 | Capture, Injection & Safe Clipboard Engine | Polishly.WindowsIntegration, Win32/UIA, Guarded Clipboard, Hotkeys, Security guards | M1 | DONE | d3e4a5b7-7b7b-4d03-b91b-c62784470609 |
| M3 | Providers & Secure Credentials | Polishly.Providers (OpenAI, Anthropic, Groq, Cerebras, Demo), Credential Manager, Redacted Logger | M1, M2 | DONE | 22b880c7-ec14-4638-b93f-ff30d38d2f3d |
| M4 | Non-Activating Diff Popup & WPF UI | Polishly.App WPF UI, WS_EX_NOACTIVATE popup, tray icon, settings, DPI, onboarding | M1, M2, M3 | DONE | 649a237b-7cf2-4062-921e-566d43b22a88 |
| M5 | Final E2E Test Integration & Hardening | 100% E2E test pass (Tiers 1-4) + Tier 5 Adversarial Coverage Hardening & Forensic Audit | M0, M1-M4 | DONE | 96e8aeed-680d-4f63-8137-1935eac17105 |

## Interface Contracts

### `Polishly.Core` ↔ `Polishly.WindowsIntegration`
- `ICaptureEngine.CaptureSelectionAsync() -> Task<SelectionContext>`
- `IInjectorEngine.InjectTextAsync(TargetContext context, string newText) -> Task<InjectionResult>`
- `IClipboardTransaction.ExecuteSafePasteAsync(string textToPaste) -> Task<ClipboardTransactionResult>`
- `ICredentialStore.SaveApiKeyAsync(string providerId, string apiKey) -> Task`
- `ICredentialStore.GetApiKeyAsync(string providerId) -> Task<string?>`
- `ISensitiveFieldDetector.IsSensitiveField(TargetWindow window, AutomationElement element) -> SensitiveFieldStatus`

### `Polishly.Core` ↔ `Polishly.Providers`
- `IAiProvider.StreamRewriteAsync(RewriteRequest request, CancellationToken ct) -> IAsyncEnumerable<RewriteToken>`
- `IAiProvider.ValidateCredentialsAsync(string apiKey) -> Task<ValidationResult>`

## Code Layout
- Target SDK: .NET 10 LTS (`net10.0-windows`)
- WPF UI with Native Fluent Styling
- C# 13 language features, nullable reference types enabled, implicit usings enabled
