# Polishly

Native macOS menu-bar assistant for explicitly invoked AI rewriting. The interaction model and visual reference are in `mockup.html`; the product and safety requirements are in `PLAN.md`.

## Run

```sh
xcodebuild -project Polishly.xcodeproj -scheme Polishly -configuration Debug -derivedDataPath .build build
open .build/Build/Products/Debug/Polishly.app
```

Do not disable code signing for local builds. Xcode's **Sign to Run Locally** identity keeps the proper bundle identifier attached to the app. Because this is still an ad-hoc debug signature, macOS may require the Accessibility row to be replaced after the executable changes. A production Developer ID signature provides the stable identity used by a distributed build.

The first launch checks only Accessibility. Polishly starts in local **demo mode** and never unlocks the login Keychain automatically. Settings can connect OpenAI, Groq, Cerebras, or Anthropic. A typed key becomes active for that session and stays in memory unless you explicitly choose **Save to Keychain…**; **Load Saved Key…** is also always an explicit action.

Default provider models are editable in Settings:

- OpenAI: `gpt-5.6-sol` through the Responses API
- Groq: `llama-3.3-70b-versatile` through Chat Completions
- Cerebras: `gpt-oss-120b` through Chat Completions
- Anthropic: `claude-haiku-4-5` through Messages

If macOS shows Accessibility as enabled but Polishly reports otherwise, remove the stale Polishly row in **System Settings → Privacy & Security → Accessibility**, add the exact app at `.build/Build/Products/Debug/Polishly.app`, and switch it on. This is expected after some ad-hoc debug rebuilds; the Settings status refreshes automatically.

## Manual Phase 1 validation

1. Grant Accessibility access, select text in Notes, and use `Control-Option-Space`.
2. Check that the card is anchored to the selection and that tabs, custom revision, copy, Escape, and Accept behave correctly.
3. Test paste-back in Notes. The app tries an AX write first, then a guarded clipboard transaction only if the originating app still has focus.
4. Run the Teams checks in `PLAN.md` before treating it as supported; context capture is intentionally not enabled as a shipped Promise B feature.
5. Test clipboard safety with rich clipboard content and with a copy made during the paste window. The original clipboard is restored only if the pasteboard is still in Polishly's post-write state.

Automated build validation:

```sh
xcodebuild -project Polishly.xcodeproj -scheme Polishly -configuration Debug -derivedDataPath .build build
```
