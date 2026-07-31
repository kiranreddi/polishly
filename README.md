<p align="center">
  <img src="docs/images/icon.png" width="96" alt="Polishly icon" />
</p>

<h1 align="center">Polishly</h1>

<p align="center">
  <strong>One shortcut. One diff. Zero cloud middlemen.</strong><br>
  Rewrite selected text wherever you type — privately, in place, on Mac or Windows.
</p>

<p align="center">
  <a href="https://polishly.info">Website</a> ·
  <a href="https://github.com/kiranreddi/polishly/releases/latest">Latest release</a> ·
  <a href="https://github.com/kiranreddi/polishly/issues">Issues</a>
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-13b8a4.svg" alt="MIT License" /></a>
  <a href="https://github.com/kiranreddi/polishly"><img src="https://img.shields.io/github/stars/kiranreddi/polishly?style=flat&color=13b8a4" alt="GitHub stars" /></a>
  <img src="https://img.shields.io/badge/macOS-14%2B-13b8a4.svg" alt="macOS 14+" />
  <img src="https://img.shields.io/badge/Windows-10%2B%20preview-718cff.svg" alt="Windows 10+ preview" />
</p>

<p align="center">
  <img src="docs/video/polishly-demo.gif" width="720" alt="Polishly selecting text, generating a rewrite, and showing an inline diff" />
</p>

## The idea

Most writing assistants make you leave the app, paste text into a cloud editor, and trust an invisible replacement. Polishly keeps the useful part and removes the ceremony:

```text
Select text  →  press one hotkey  →  review the diff  →  Accept or Copy
```

Polishly is a free, open-source companion that sits in the menu bar on Mac or the system tray on Windows. It only reads the selection after you invoke it, sends the text directly to the AI provider you choose, and shows exactly what will change before anything is replaced.

## Why it feels different

- **In-place rewrites.** Work in Notes, Mail, Slack, Teams, browsers, and other accessible text fields.
- **Review before replace.** Polishly computes a word-level diff locally so you can see the edit before accepting it.
- **Bring your own key.** Use OpenAI, Anthropic, Groq, Cerebras, or the offline demo flow.
- **No Polishly cloud.** There is no Polishly rewrite backend, account, subscription, or text warehouse in the middle.
- **Explicit by design.** Nothing is read or sent until you press the shortcut.
- **Native companions.** SwiftUI + AppKit on Mac; a Windows tray companion with UI Automation, guarded clipboard fallback, and Credential Manager storage.

## Before / after

**Instruction: make it clearer**

> **Before:** i have sent the mail let see what he will tell
>
> **After:** I've sent the email — let's see how he responds.

**Instruction: translate to Spanish**

> **Before:** I think we should move the meeting to next week.
>
> **After:** Creo que deberíamos posponer la reunión para la próxima semana.

The important part is not a particular tone. It is the small loop: invoke, inspect, decide.

## Choose your platform

| Platform | Status | Shortcut | Download / build |
|---|---|---|---|
| **macOS 14+** | Release build | `⌃⌥Space` | [Download the latest DMG](https://github.com/kiranreddi/polishly/releases/latest) |
| **Windows 10+** | Preview companion | `Ctrl + Shift + P` | [Download source ZIP](https://github.com/kiranreddi/polishly/archive/refs/heads/main.zip) · [Build instructions](windows/) |

The Mac release is signed and notarized. The Windows companion is available as a source preview while the signed Windows installer package is being prepared.

## Quick start

### macOS

1. Download the latest `.dmg` from [Releases](https://github.com/kiranreddi/polishly/releases/latest).
2. Drag `Polishly.app` into `/Applications` and open it.
3. Grant Accessibility access in **System Settings → Privacy & Security → Accessibility**.
4. Open Settings, choose a provider, and paste your API key — or stay in Demo mode.
5. Select text anywhere and press `⌃⌥Space`.

### Windows preview

The Windows companion is a native tray app. It stores keys in Windows Credential Manager and uses UI Automation with a guarded clipboard fallback where needed.

```powershell
git clone https://github.com/kiranreddi/polishly.git
cd polishly
dotnet build windows/Polishly.Windows.sln
dotnet run --project windows/src/Polishly.App/Polishly.App.csproj
```

On Windows, open **Settings**, choose your provider, paste the key, and press **Save settings**. Select text in a supported field and press `Ctrl + Shift + P` to review a rewrite.

## Connect an AI provider

The app is free. You bring the provider key and pay only the provider if you choose a paid plan.

| Provider | Good starting point | Key |
|---|---|---|
| **[Groq](https://console.groq.com/keys)** | Fast free-tier experiments; no card is required for signup | Starts with `gsk_` |
| **[Cerebras](https://cloud.cerebras.ai/)** | Very fast inference and generous trial access | Provider console |
| **[OpenAI](https://platform.openai.com/api-keys)** | OpenAI models | Provider console |
| **[Anthropic](https://console.anthropic.com/)** | Claude models | Provider console |
| **Demo mode** | Try capture → diff → accept without a network request | No key |

### Groq in under two minutes

1. Open [console.groq.com/keys](https://console.groq.com/keys) and create an account.
2. Select **Create API Key**, name it `polishly`, and copy it immediately.
3. In Polishly, open **Settings → AI Provider**, choose **Groq**, and paste the key.
4. Save the settings, select text, and invoke the shortcut.

Provider free tiers have their own rate limits and policies. Check the provider console for current limits; Polishly never bills you.

## Build from source

### Mac

Requires macOS 14+, Xcode, and [XcodeGen](https://github.com/yonaskolb/XcodeGen).

```sh
brew install xcodegen
git clone https://github.com/kiranreddi/polishly.git
cd polishly
xcodegen generate
xcodebuild -project Polishly.xcodeproj -scheme Polishly -configuration Debug build
```

For a distributable Mac build:

```sh
./scripts/package-release.sh
```

This creates `dist/Polishly.app` and `dist/Polishly-1.0.0.dmg`.

### Windows

Requires the .NET 10 SDK. The solution and companion projects are under [`windows/`](windows/).

```powershell
dotnet build windows/Polishly.Windows.sln
dotnet run --project windows/src/Polishly.App/Polishly.App.csproj -- --demo-rewrite --text "a rough sentence to improve"
```

The demo command exercises the headless rewrite path without an API key. The full tray, hotkey, and UI Automation experience needs a real Windows host.

## Test it

Mac tests:

```sh
xcodegen generate
xcodebuild -project Polishly.xcodeproj -scheme Polishly -configuration Debug test
```

Windows tests use the repository's lightweight runners:

```powershell
dotnet run --project windows/tests/Polishly.Core.Tests/Polishly.Core.Tests.csproj
dotnet run --project windows/tests/Polishly.Platform.Tests/Polishly.Platform.Tests.csproj
dotnet run --project windows/tests/Polishly.AppCompatibility.Tests/Polishly.AppCompatibility.Tests.csproj
```

The Windows CI workflow runs the Release build, all three test assemblies, and the demo rewrite path on `windows-latest`.

## How it works

1. **Capture** — Accessibility on Mac or UI Automation on Windows reads the selected text only after the hotkey.
2. **Rewrite** — The configured provider streams a response directly to the app.
3. **Diff** — Polishly computes a local word-level diff.
4. **Replace** — Accept writes the approved result back into the active field; Copy leaves the original untouched.

Sensitive apps and password fields are guarded. API keys stay in macOS Keychain or Windows Credential Manager rather than plaintext preferences.

## Project map

```text
Sources/                 macOS app, providers, diff engine, state machine
windows/src/             Windows companion, core, providers, platform adapters
windows/tests/            Windows unit and compatibility runners
website/                 Product site and platform download page
docs/                    Screenshots, demo media, and planning notes
```

## Privacy

Polishly has no rewrite server. When you invoke a rewrite, the selected text goes from your device directly to the provider configured in Settings using your key. Nothing is sent before invocation, and the source is MIT licensed so you can inspect the workflow yourself.

## Contributing

Issues, product feedback, screenshots, and pull requests are welcome. If you find a platform-specific problem, include:

- operating system and version;
- Polishly version or commit;
- the host app where it happened;
- whether the issue occurred during capture, streaming, diff review, or replacement.

Please do not paste API keys or private text into issues.

## License

[MIT](LICENSE)

<p align="center">
  <a href="https://polishly.info">polishly.info</a> ·
  <a href="https://github.com/kiranreddi/polishly">GitHub</a> ·
  <a href="https://github.com/kiranreddi/polishly/issues">Report a problem</a>
</p>
