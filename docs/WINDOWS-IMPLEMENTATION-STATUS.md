# Polishly for Windows — implementation status

**Audited:** July 30, 2026
**Contract:** [`WINDOWS-PLAN.md`](WINDOWS-PLAN.md)
**Release state:** Implementation candidate; external qualification is not yet complete

This document separates implemented behavior from evidence that must be collected
on interactive Windows machines. Headless tests are never counted as real
application compatibility results.

## Phase audit

| Phase | Implemented | Qualification still required |
|---|---|---|
| W0 — Contract and solution | .NET/WPF solution, CI, provider/diff/error contracts; clean Windows CI build | Re-run CI for every candidate commit |
| W1 — Capture and replacement | UIA selection and bounds, all-or-nothing clipboard materialization, exact process/window/field revalidation, Copy fallback | 20-attempt Notepad and Teams matrices |
| W2 — Functional MVP | Popup mode controls, revise, cancellable streaming/regeneration, local diff, all five providers, Credential Manager, persistent settings, model validation, connection tests, hotkey, theme, pause and blocklist | One live credential-and-rewrite smoke run per cloud provider |
| W3 — Popup/DPI/accessibility | Per-Monitor V2 manifest, physical-pixel positioning, actual selection/window anchor, mixed-DPI work areas, negative coordinates, dynamic height, Escape/click-outside hooks, high-contrast base and accessible names | Physical 100/125/150/175/200% one- and two-monitor matrix |
| W4 — Application expansion | Explicit conservative capability profiles for every planned application | Interactive compatibility matrix and documented limitations |
| W5 — Security/onboarding | Six-step onboarding, guided real rewrite, password/elevation/blocklist refusal, local non-secret settings, Credential Manager keys, packaged and unpackaged startup handling | Fresh Windows-account onboarding run |
| W6 — Packaging/beta | Reproducible x64 MSIX bundle script, signing support, AppInstaller feed, checksums, release workflow and lifecycle test script; unsigned artifact build verified in CI | Production certificate, signed artifacts, lifecycle/Defender/SmartScreen runs, hosted download, and 10–20-person beta |

## Automated evidence

[Windows CI run 30588751196](https://github.com/kiranreddi/polishly/actions/runs/30588751196)
recorded a zero-warning, zero-error native WPF build, 93 core tests, 159
platform tests, 35 compatibility-contract tests, a passing headless rewrite,
and successful unsigned MSIX, MSIX bundle, AppInstaller, and checksum creation.
That is 287 passing automated tests. The compatibility-contract suite verifies
profiles and orchestration; it does not claim interactive application success.

Run from the repository root:

```powershell
dotnet build windows/Polishly.Windows.sln --configuration Release -warnaserror
dotnet run --project windows/tests/Polishly.Core.Tests/Polishly.Core.Tests.csproj --configuration Release
dotnet run --project windows/tests/Polishly.Platform.Tests/Polishly.Platform.Tests.csproj --configuration Release
dotnet run --project windows/tests/Polishly.AppCompatibility.Tests/Polishly.AppCompatibility.Tests.csproj --configuration Release
dotnet run --project windows/src/Polishly.App/Polishly.App.csproj --configuration Release -- --demo-rewrite --text "Draft text"
```

Live provider smoke tests use an environment variable so keys never appear in
arguments or logs:

```powershell
$env:POLISHLY_PROVIDER_API_KEY = "<temporary key>"
dotnet run --project windows/src/Polishly.App/Polishly.App.csproj --configuration Release -- --provider-smoke openai
Remove-Item Env:\POLISHLY_PROVIDER_API_KEY
```

Repeat for `anthropic`, `groq`, and `cerebras`, or dispatch the Windows Companion
workflow with **Run live provider validation and rewrite smoke tests** enabled.

## Interactive evidence

Run [`windows/qa/Run-Compatibility-Matrix.ps1`](../windows/qa/Run-Compatibility-Matrix.ps1)
on Windows. It records all 20 attempts per application and computes capture,
replacement, wrong-target, clipboard, and recovery results without inventing
successes.

For release artifacts:

1. Build and sign two versions with
   [`Build-Package.ps1`](../windows/packaging/Build-Package.ps1).
2. Run [`Test-PackageLifecycle.ps1`](../windows/qa/Test-PackageLifecycle.ps1)
   from a clean Windows test account.
3. Verify both artifacts with `signtool verify /pa /all`.
4. Scan with Microsoft Defender and record SmartScreen behavior.
5. Publish the bundle, `.appinstaller`, `SHA256SUMS.txt`, release notes, and the
   completed compatibility report.

The MVP must not be labelled complete until every qualification cell above has
recorded passing evidence.
