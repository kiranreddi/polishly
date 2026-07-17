# Polishly

Native macOS menu-bar assistant for explicitly invoked AI rewriting. The interaction model and visual reference are in `mockup.html`; the product and safety requirements are in `PLAN.md`.

## Run

```sh
xcodebuild -project Polishly.xcodeproj -scheme Polishly -configuration Debug CODE_SIGNING_ALLOWED=NO build
open .build/Build/Products/Debug/Polishly.app
```

The first launch offers Accessibility setup and an optional Anthropic API key stored in the macOS Keychain. **Use demo mode** keeps rewrites on-device and gives the UI a usable no-key path.

## Manual Phase 1 validation

1. Grant Accessibility access, select text in Notes, and use `Option-Space`.
2. Check that the card is anchored to the selection and that tabs, custom revision, copy, Escape, and Accept behave correctly.
3. Test paste-back in Notes. The app tries an AX write first, then a guarded clipboard transaction only if the originating app still has focus.
4. Run the Teams checks in `PLAN.md` before treating it as supported; context capture is intentionally not enabled as a shipped Promise B feature.
5. Test clipboard safety with rich clipboard content and with a copy made during the paste window. The original clipboard is restored only if the pasteboard is still in Polishly's post-write state.

Automated build validation:

```sh
xcodebuild -project Polishly.xcodeproj -scheme Polishly -configuration Debug -derivedDataPath .build CODE_SIGNING_ALLOWED=NO build
```
