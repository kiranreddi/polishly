# AGENTS.md

## Cursor Cloud specific instructions

### What runs on this Linux cloud VM

| Component | Runnable here? | Notes |
|---|---|---|
| Windows companion (`windows/`) | **Yes (headless)** | .NET 10 SDK required. Cross-platform stubs disable WPF/Win32; core + providers + tests + `--demo-rewrite` work. Full tray/hotkey/UIA GUI needs a real Windows host. |
| Website (`website/`) | **Yes** | Vanilla static site; `cd website && npm run dev` → http://127.0.0.1:4173 |
| macOS app (`Sources/`, Xcode) | **No** | Requires macOS + Xcode + XcodeGen |

### Windows companion (primary on `feature/windows-companion`)

Standard commands are documented in `TEST_INFRA.md` / `TEST_READY.md`. Quick reference:

```bash
# Ensure SDK on PATH (installed under ~/.dotnet in this environment)
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"

dotnet build windows/Polishly.Windows.sln

# Custom xunit-compatible runners (OutputType=Exe) — prefer `dotnet run`, not `dotnet test`
dotnet run --project windows/tests/Polishly.Core.Tests/Polishly.Core.Tests.csproj
dotnet run --project windows/tests/Polishly.Platform.Tests/Polishly.Platform.Tests.csproj
dotnet run --project windows/tests/Polishly.AppCompatibility.Tests/Polishly.AppCompatibility.Tests.csproj

# Headless end-to-end rewrite (capture → DemoProvider stream → word-diff → inject → Accepted)
dotnet run --project windows/src/Polishly.App/Polishly.App.csproj -- --demo-rewrite --text "your text"
```

**Gotchas:**

- Projects fall back to `net10.0` / `UseWPF=false` when `OS != Windows_NT`. Forcing `-p:OS=Windows_NT -p:EnableWindowsTargeting=true` cross-compiles the Windows TFM (validates WPF code paths compile) but the resulting binary still cannot run the GUI on Linux.
- There are legacy duplicate types under `Polishly.Core` (e.g. old `RewriteStateMachine` + `RewriteTrigger` vs `Polishly.Core.StateMachine.*`). Prefer the `StateMachine` namespace used by the App/ViewModels; qualify types when both exist (`WordDiffEngine` is ambiguous).
- After Accept, paste handlers must fire `RewriteEvent.ReplaceSuccess` / `ReplaceFailed` to leave `Replacing` and reach `Accepted` / `Failed`.
- No NuGet test packages: runners live in `windows/tests/XunitFramework.cs` + `TestRunner.cs`.

### Website

```bash
cd website && npm install && npm run dev   # build + serve dist/client on :4173
```

No lint/test scripts in `website/package.json`. Clean URLs without `.html` are a Vercel/worker concern; the local `scripts/dev.mjs` serves files as-is (`/groq-setup.html` works; `/groq-setup` may 404).
