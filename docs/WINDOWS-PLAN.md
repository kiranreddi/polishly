# Polishly for Windows — Implementation Plan

**Status:** Planned  
**Last updated:** July 2026  
**Target:** A trustworthy Windows 11 MVP with parity for Polishly's core rewrite workflow

## 1. Goal

Build a native Windows companion to Polishly for macOS. The Windows app must preserve the same product promises:

- Rewrite text only when the user explicitly invokes Polishly.
- Show a real local diff before replacing anything.
- Replace text in the original field only when it can be done safely.
- Never paste into an application or field that cannot be proven to be the original target.
- Support OpenAI, Anthropic, Groq, Cerebras, and on-device Demo mode.
- Store API keys in the operating system's credential store.
- Run from the system tray without continuously scanning what the user types.

The largest engineering risk is reliable selection capture and replacement across Windows applications. Recreating the popup is not the difficult part.

## 2. Recommended platform

| Area | Decision |
|---|---|
| Language | C# on .NET 10 LTS |
| UI | WPF with the native Windows Fluent theme |
| Platform integration | Win32, Windows UI Automation, and Windows App SDK where useful |
| Initial operating system | Windows 11 x64 |
| Packaging | Signed MSIX bundle plus an `.appinstaller` update feed |
| Distribution | Direct download first; Microsoft Store evaluation after beta |
| Repository layout | A sibling `windows/` solution in this repository |

WPF is preferred over Electron, Tauri, or MAUI because Polishly needs precise control over focus, non-activating windows, clipboard formats, native accessibility APIs, system hotkeys, and monitor coordinates.

Current Microsoft platform references:

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- [WPF Fluent theme](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90)
- [MSIX overview](https://learn.microsoft.com/en-us/windows/msix/overview)
- [App Installer automatic updates](https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview)

## 3. Proposed solution structure

```text
windows/
├── Polishly.Windows.sln
├── src/
│   ├── Polishly.App/
│   │   ├── Tray menu
│   │   ├── Settings
│   │   ├── Onboarding
│   │   └── Rewrite popup
│   ├── Polishly.Core/
│   │   ├── Rewrite state machine
│   │   ├── Provider configuration
│   │   ├── Prompt builder
│   │   ├── Diff engine
│   │   └── Application capability rules
│   ├── Polishly.WindowsIntegration/
│   │   ├── UI Automation capture
│   │   ├── Clipboard transaction
│   │   ├── Text injection
│   │   ├── Global hotkey
│   │   ├── Popup positioning
│   │   ├── Foreground-window tracking
│   │   └── Credential Manager
│   └── Polishly.Providers/
│       ├── OpenAI
│       ├── Anthropic
│       ├── Groq
│       └── Cerebras
└── tests/
    ├── Polishly.Core.Tests/
    ├── Polishly.Platform.Tests/
    └── Polishly.AppCompatibility.Tests/
```

Do not attempt to share Swift UI or platform-integration code. Share behavior through provider contracts, prompt fixtures, diff fixtures, error-state definitions, and cross-platform test vectors.

## 4. macOS-to-Windows mapping

| macOS implementation | Windows equivalent |
|---|---|
| Accessibility AX API | Windows UI Automation `TextPattern` and `TextPattern2` |
| `NSPanel` popup | WPF window using `WS_EX_NOACTIVATE` and `WS_EX_TOOLWINDOW` |
| Global keyboard listener | `RegisterHotKey` and `WM_HOTKEY` |
| `NSPasteboard.changeCount` | `GetClipboardSequenceNumber` |
| Synthesized `Cmd+C` / `Cmd+V` | `SendInput` with `Ctrl+C` / `Ctrl+V` |
| macOS Keychain | Windows Credential Manager |
| Menu-bar item | System tray icon using `Shell_NotifyIcon` |
| Launch at Login | Packaged startup task |
| AX selection bounds | UI Automation text-range bounding rectangles |
| `NSScreen` coordinates | Per-Monitor V2 DPI-aware Win32 coordinates |

Relevant Windows APIs:

- [`RegisterHotKey`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [`GetClipboardSequenceNumber`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboardsequencenumber)
- [`CredWrite`](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew)

## 5. Core rewrite sequence

1. Record the foreground process, window handle, focused automation element, and selection.
2. Capture selected text using UI Automation when supported.
3. Fall back to a guarded clipboard copy transaction when UI Automation is unreliable.
4. Capture selection bounds or derive a safe popup location near the active field.
5. Open a non-activating Polishly popup immediately with a loading state.
6. Send selected text to the configured provider only after explicit invocation.
7. Stream the rewrite into the popup.
8. Compute the word-level diff locally.
9. On Accept, prove the original process, window, and field are still valid.
10. Replace through UI Automation or a guarded paste transaction.
11. Fall back to Copy when safe in-place replacement cannot be confirmed.

## 6. Clipboard transaction requirements

The Windows clipboard fallback must preserve the same trust guarantees as the Mac app:

1. Snapshot every available clipboard format, not only plain text.
2. Record `GetClipboardSequenceNumber` before changing the clipboard.
3. Materialize delayed-rendering clipboard formats where required.
4. Write the temporary Polishly text.
5. Verify the original application is still foreground.
6. Send `Ctrl+V` using `SendInput`.
7. Restore the original clipboard only if the sequence number still matches the Polishly write.
8. Never restore over content copied by the user during the transaction.
9. Timeout safely and show an explicit recovery state.
10. Never synthesize paste into a different window or field.

## 7. Delivery phases

### Phase W0 — Product and platform contract

**Estimated duration:** 3–5 days

- Freeze the Windows MVP feature list.
- Define shared provider, prompt, diff, and error behavior.
- Export reusable provider and diff test fixtures from the Mac implementation.
- Establish the Windows solution and CI build.
- Define popup, onboarding, tray, and settings behavior.
- Confirm supported Windows versions and initial architecture.

**Exit gate:** The solution builds on a clean Windows machine and the cross-platform behavior contract is documented.

### Phase W1 — Capture and replacement spike

**Estimated duration:** 2 weeks

Initial target applications:

- Windows Notepad
- Microsoft Teams

Implement foreground tracking, a configurable global hotkey, UI Automation capture, selection bounds, a guarded clipboard fallback, safe text injection, Copy fallback, a basic anchored popup, and diagnostic logging that excludes selected text and credentials.

**Exit gate for each target application:**

| Criterion | Target |
|---|---:|
| Selection capture | At least 18/20 attempts |
| Replacement in the correct field | At least 18/20 attempts |
| Paste into wrong application or field | 0 occurrences |
| Clipboard corruption | 0 occurrences |
| Visible recovery state on failure | 100% |

Do not proceed until Teams passes this gate.

### Phase W2 — Functional MVP

**Estimated duration:** 2–3 weeks

- Improve, Concise, Friendly, and Expand modes.
- One-off Revise with AI instructions.
- Streaming provider responses.
- Local word-level diffing.
- Accept, Copy, Retry, and Regenerate.
- OpenAI, Anthropic, Groq, and Cerebras.
- Provider model and API-key validation.
- Demo mode and provider connection testing.
- Settings window and tray menu.
- Pause Polishly and per-app enable/disable.
- Configurable hotkey.
- Light and dark modes.
- Clear network, authentication, rate-limit, offline, and replacement errors.

API keys must be stored as generic Windows Credential Manager credentials, never in settings files.

**Exit gate:** All provider contract tests pass, one real smoke test passes per provider, no key appears in logs, and original text remains unchanged until Accept succeeds.

### Phase W3 — Popup, focus, DPI, and monitor reliability

**Estimated duration:** 1–2 weeks

- Enable Per-Monitor V2 DPI awareness.
- Handle monitors with different scale factors and negative coordinates.
- Flip the popup above or below the selection based on available work area.
- Reposition while streamed content changes popup height.
- Keep the popup out of the taskbar.
- Avoid stealing focus during normal rewrites.
- Support Escape, click-outside dismissal, and keyboard navigation.
- Add screen-reader labels and high-contrast behavior.

Revise with AI requires temporary keyboard focus:

1. Preserve the original window and automation element.
2. Temporarily activate the Polishly instruction input.
3. Return focus to the source application after submission.
4. Revalidate the source window and field before replacement.
5. Fall back to Copy if the original target cannot be proven.

**Exit gate:** Placement and interaction pass at 100%, 125%, 150%, 175%, and 200% display scaling across one- and two-monitor configurations.

### Phase W4 — Supported-application expansion

**Estimated duration:** 2–3 weeks

Test and classify:

- Notepad
- Microsoft Teams
- Outlook Classic
- New Outlook
- Microsoft Word
- Slack
- Chrome
- Edge
- Gmail in Chrome and Edge
- VS Code
- OneNote

Each application receives a capability profile:

```text
capture method
replacement method
selection bounds availability
automatic trigger support
known limitations
context extractor availability
```

No application is marketed as supported until it passes its compatibility matrix.

**Exit gate:** At least 95% capture/replacement success for supported native applications, at least 90% for Electron and browser applications, zero wrong-window pastes, and explicit handling of every failure.

### Phase W5 — Security and onboarding

**Estimated duration:** 1 week

Windows onboarding:

1. Welcome and privacy explanation.
2. Choose a provider or Demo mode.
3. Add and test the API key.
4. Choose a global hotkey.
5. Complete a guided rewrite in a test field.
6. Optionally enable startup with Windows.

Security requirements:

- Block password fields using UI Automation password properties.
- Maintain a sensitive-application blocklist.
- Do not run Polishly as administrator by default.
- Detect elevated target applications.
- For elevated targets, explain the limitation and offer Copy.
- Keep diagnostic reporting opt-in and exclude selected text.

### Phase W6 — Packaging and private beta

**Estimated duration:** 1–2 weeks

- Produce a signed x64 MSIX bundle.
- Configure a trusted code-signing certificate.
- Add `.appinstaller` automatic updates.
- Test clean install, update, rollback, repair, and uninstall.
- Verify Windows Defender and SmartScreen behavior.
- Add a Windows download route to the Polishly website.
- Publish checksums, release notes, and a compatibility page.
- Run a private beta with 10–20 users across different target apps and display configurations.

## 8. Features deferred until after the MVP

- Automatic Grammarly-style trigger on text selection.
- Reading surrounding email or conversation context.
- Persistent rewrite history.
- Personal writing style learning.
- Local models.
- ARM64 release.
- Microsoft Store publication.
- `uiAccess=true` or interaction with elevated applications.
- Windows/macOS settings synchronization.

Automatic selection triggers should follow the proven hotkey workflow. UI Automation selection-change events are inconsistent across Electron and browser applications and must be enabled per application only after validation.

## 9. Performance targets

| Interaction | Target |
|---|---:|
| Hotkey to popup skeleton | Under 150 ms |
| First streamed token | Under 1.2 seconds, provider permitting |
| Completed short rewrite | Under 3 seconds |
| Accept to UI Automation replacement | Under 250 ms |
| Clipboard replacement | Under 500 ms |
| Cold launch to tray icon | Under 2 seconds |
| Idle CPU | Effectively 0% |

Polishly must not continuously scan text in the background.

## 10. Agent implementation order

The implementation agent should work in strict, gated phases:

1. Scaffold the Windows solution.
2. Build Notepad capture and replacement.
3. Build Teams clipboard fallback.
4. Run and record the numeric Phase W1 test matrix.
5. Implement the popup and local diff.
6. Port all provider clients and validation.
7. Implement Windows Credential Manager storage.
8. Build onboarding, settings, and tray behavior.
9. Run multi-monitor and DPI testing.
10. Expand supported applications individually.
11. Package and sign the application.
12. Conduct private beta QA.
13. Only then add automatic selection triggers and context extraction.

Every phase ends with automated tests, real visual inspection, a compatibility report, a focused commit, and review before the next phase.

## 11. Definition of done

The Windows MVP is complete only when:

- Notepad, Teams, Outlook, Word, Slack, Chrome, Edge, VS Code, and OneNote have explicit compatibility results.
- Every marketed supported application meets its capture and replacement target.
- No QA run pastes into the wrong field or application.
- Clipboard-format preservation tests pass without data loss.
- Provider keys survive restart through Credential Manager and never appear in logs.
- Popup placement passes the DPI and multi-monitor matrix.
- The app is signed, packaged, installable, updateable, repairable, and removable.
- Onboarding can be completed without developer tools.
- The beta release passes a complete end-to-end rewrite using a real provider.

## 12. Expected schedule

A realistic estimate is **9–12 weeks** for a trustworthy Windows MVP. A basic demonstration can be built sooner, but capture, clipboard, focus, DPI, and cross-application reliability determine whether Polishly is safe to ship.

The governing release rule is:

> Never paste unless Polishly can prove it is returning to the exact source application and field.
