# Project "Polishly" — System-Wide AI Rewriting Assistant for Mac

*Complete product & technical plan — v1.2, July 2026*
*(Name finalized: Polishly — cleared on domain, App Store, and general web search; see naming research log for the full trail of rejected candidates.)*

*v1.1 changelog: revised after review to split the MVP into a reliable core promise vs. an optional context enhancement, add a measurable Phase 0 test matrix, specify clipboard transaction safety, resolve a Phase 1/2 scope contradiction, add missing technical decisions (hotkey conflicts, API key handling, local diffing, context limits/redaction, failure states), and flag pricing/cost figures as unvalidated assumptions. See §10 for the full list of what changed and why.*
*v1.2 changelog: product named Polishly (formerly working codename "Refract").*

---

## 1. Vision

Select text anywhere on your Mac — Mail, Teams, Slack, Outlook, browser — hit one shortcut, and a small floating card appears with a rewrite. Accept it and the text is replaced in place. No copy-paste, no switching apps, no browser extension.

The wedge vs Grammarly: Grammarly is a grammar checker with AI bolted on. Polishly is **explicitly-invoked, context-aware rewriting** — where the surrounding conversation is readable, it rewrites *for that situation*, not just for grammar. Where it isn't, it still nails the reliable core: turn a rough draft into a clean one, in place, on request.

Your own Grammarly screenshot is the exact use case: you typed a quick status update ("i have sent the mail let see what he will...") and wanted it cleaned into professional English before sending to Brad/Justin. That moment — "make this sound right before I hit send" — is the entire MVP.

## 2. Market snapshot (July 2026)

| Product | Approach | Weakness we exploit |
|---|---|---|
| Grammarly Desktop | System-wide underlines via Accessibility API, popup cards | Heavy, subscription fatigue ($30/mo Pro), always-on scanning feels invasive, weak conversation context |
| Apple Intelligence Writing Tools | Built into macOS, free | Generic rewrites, no thread context, limited tones, only in apps that adopt the API |
| WordWand / RewriteBar / Elephas | Select + shortcut → AI action menu | Utility feel, no context capture, no in-place diff, dated UI |
| LanguageTool / Grambo / Stanza | Grammar-focused, some local models | Not rewrite/context focused |

Gap: nobody does **thread-aware rewriting with a clean in-place diff experience** — but see §3, that gap is only provable app-by-app, not claimed universally.

## 3. MVP scope — two separate promises

Prior draft treated "select → rewrite → replace" and "read the conversation above" as one feature. They aren't — the first is a reliable, universal mechanism (selection + clipboard exist everywhere). The second depends on each app's Accessibility tree being walkable, which is inconsistent across Teams, Slack, browsers, and Outlook and cannot be promised universally without testing each one. Treating it as solved before proving it per-app was the single biggest risk in v1.0 of this plan.

### Promise A — the reliable core (must work everywhere, day one)

1. User selects text in any app.
2. Presses global hotkey (default `⌥ Space`, see §4.5 for conflict handling) — or clicks a subtle trigger near the selection.
3. Card appears anchored to the selection, showing a rewrite with a real inline diff (locally computed — see §4.5).
4. Tone tabs: **Improve · Concise · Friendly · Expand**. A "Revise with AI" control opens a one-off free-text instruction box (see below on Phase 1 vs Phase 2 scope for instructions).
5. **Accept** replaces the text in place via the clipboard-transaction mechanism in §4.2. **Esc** dismisses.

This promise never depends on reading anything other than the text the user selected. It must work in every target app or the app doesn't ship for that app.

### Promise B — context enhancement (opt-in per app, explicitly disclosed)

Where an app's Accessibility tree allows it, Polishly additionally reads nearby visible text (the email thread above, the last few chat messages) and passes it to the model as context. This is:

- **Off by default until validated per app.** Each supported app gets its own tested "context extractor" (see §4.5) or it simply doesn't get one — Promise A still works.
- **Always disclosed in the card**, stating exactly what was read, e.g. "Using 2 visible Teams messages above as context" — never a vague claim of broader awareness. If no context is available: "No thread context available in Notes — using only your selected text." (Both states are shown in `mockup.html`.)

Explicitly **out** of MVP: always-on underlining/scanning, browser extension, Windows, plagiarism/tone-detector/score features, team features, persistent custom instructions or style learning (that's Phase 2, see §7).

## 4. How it works technically

### 4.1 Core mechanism: macOS Accessibility API (AX)

Same foundation as Grammarly Desktop. With the user's one-time grant in *System Settings → Privacy & Security → Accessibility*, the app can:

- Find the focused UI element: `AXUIElementCopyAttributeValue(systemWide, kAXFocusedUIElementAttribute)`
- Read selected text: `kAXSelectedTextAttribute`; its screen position: `kAXBoundsForRangeParameterizedAttribute` (to anchor the popup)
- Read surrounding/visible text for context (Promise B only, app-specific): `kAXValueAttribute` on the field, plus walking sibling elements (message list in Mail/Teams) — this is the part that is *not* guaranteed to work and must be validated per app before being enabled for it.
- Replace text: set `kAXSelectedTextAttribute` to the new string — works in most native apps

### 4.2 Fallback chain and clipboard transaction safety (critical for Teams)

Teams, Slack, and Electron/web apps have flaky AX support (known Electron selection-range bugs). Tiered strategy, auto-detected per app:

1. **Tier A — AX write**: set selected text attribute directly. Native apps (Mail, Notes).
2. **Tier B — paste simulation**: the workhorse, used essentially everywhere including Teams. This touches the user's clipboard, which is a trust-critical edge case and needs an explicit transaction design, not just "save and restore":
   - **Snapshot before writing.** Read and retain *all* pasteboard types present (`NSPasteboard.types`), not just plain text — rich text, HTML, file references, image data. A naive text-only save silently destroys anything else on the clipboard.
   - **Record a change-count token.** `NSPasteboard.general.changeCount` at snapshot time.
   - **Write the rewrite, synthesize `⌘V`, then restore** — but only if `changeCount` is still what we set it to. If it has changed (user copied something else in the ~100–300ms window, e.g. a fast double-copy), **do not restore** — restoring would silently clobber content the user just intentionally copied. Instead, drop the restore and surface nothing (the user's new clipboard content is preserved, which is the safer failure).
   - **Timeout safely.** If the synthesized paste doesn't land within ~800ms (no focus change detected, no edit event), abort the transaction, restore the original clipboard (if changeCount still matches), and surface an inline error state rather than leaving the target field in an unknown state.
   - **Never fire paste into an app other than the one the selection came from.** Re-verify the frontmost/focused app immediately before synthesizing `⌘V`; if focus moved (e.g. a notification stole focus), abort rather than paste into the wrong field.
3. **Tier C — copy only**: if the field rejects input entirely, show "Copied — press ⌘V" and leave the rewrite on the clipboard for the user to paste manually (no silent transaction needed here since nothing is auto-pasted).

Reading selection follows the same tiers: AX read first; fallback is synthesized `⌘C` with the same snapshot/changeCount discipline as above.

Keep a small per-app capability table (bundle ID → best read/write tier + whether a context extractor exists and is enabled), learned at runtime and shipped with sensible defaults for the top 20 apps.

### 4.3 Architecture

```
┌─ Menu-bar app (SwiftUI) ─────────────────────────────┐
│                                                      │
│  Hotkey listener (CGEventTap / KeyboardShortcuts)    │
│  Selection engine  ── AX reader / clipboard fallback │
│  Context collector ── app-specific extractors only,  │
│                       off unless validated (§4.5)    │
│  Rewrite service   ── prompt builder → Claude API    │
│                       (streaming), tone presets      │
│  Diff engine        ── local word-level diff between │
│                       original & rewritten text      │
│  Injection engine  ── AX write / clipboard-transaction│
│                       paste simulation (§4.2)        │
│  Popup UI          ── NSPanel (non-activating,       │
│                       floating), SwiftUI content     │
│  Local store       ── settings, per-app tiers,       │
│                       history (opt-in), SQLite       │
└──────────────────────────────────────────────────────┘
```

- **Language/stack**: Swift 5.10+, SwiftUI for all UI, AppKit `NSPanel` for the floating card (non-activating panel so focus stays in the target app — essential, otherwise Accept can't paste back).
- **LLM**: Claude Haiku for tone rewrites (fast/cheap), Claude Sonnet for "Revise with AI" free-text instructions. Stream tokens into the card so first words appear in <1s.
- **Prompting**: system prompt receives {app name, field type, context if available and enabled for that app, user's selected text, chosen tone/instruction}. Guardrail: model returns **only the rewritten text, no commentary, no markup** — the client is responsible for the diff (see §4.5).
- **Distribution**: Direct download + Sparkle auto-update (Accessibility API apps can't ship in Mac App Store sandbox). Notarized, hardened runtime.

### 4.4 Latency budget

Hotkey → card visible: <150 ms (show skeleton immediately). First streamed token: <1 s. Full rewrite of a paragraph: <3 s. Accept → text replaced: <200 ms (including the clipboard transaction in §4.2).

### 4.5 Technical decisions this plan was previously missing

- **Hotkey conflicts.** `⌥ Space` can collide with input-source switching on some keyboard layouts/locales and with Spotlight-adjacent bindings some users remap. Ship with a conflict check on first launch (attempt to register, detect failure, prompt for an alternate combo) and make the hotkey user-configurable from minute one, not a v2 feature.
- **API key handling.** No shared production API key embedded in the client — that's a leak/abuse vector the moment the binary is inspected. Two supported models: (a) requests proxied through a thin backend that holds the key and does per-user rate limiting/auth, or (b) BYO key stored in the macOS Keychain, never in plaintext prefs. Phase 0/1 can ship BYO-key-only to avoid standing up backend infra before the product is validated; the proxy comes with monetization in Phase 3.
- **Local diffing.** The model is instructed to return clean rewritten text only. The app computes the displayed diff itself (word-level LCS-style alignment against the original selection) rather than asking the model to emit diff markup — this is more reliable, cheaper (fewer output tokens), and is exactly what `mockup.html` v3 now does at the mock level (see the `diffWords` function).
- **Context limits, redaction, and extractors.** Context passed to the model is capped (e.g. last N messages or M characters, whichever is smaller) and app-specific: each supported app needs its own tested extractor function, not a generic "read nearby AX nodes" heuristic. Before sending, run a lightweight redaction pass for obvious secrets (things that look like API keys, tokens, card numbers) so a stray credential visible on screen doesn't get sent as "context" incidentally.
- **Failure / retry / offline states.** The card needs explicit states beyond "thinking → streaming → done": network failure (inline error, retry button, selection preserved so nothing is lost), API error/rate limit (same, with a clearer message), offline (detect and show "no connection" immediately rather than spinning), and paste-transaction timeout (see §4.2) with its own recovery path. None of these should ever leave the user's original selected text in an ambiguous or lost state — the source text is never touched until Accept succeeds.

## 5. Privacy & security

This app can read anything on screen — trust is the product's foundation.

- Text sent to the API **only** when the user explicitly invokes the hotkey. No background scanning, ever. Say this loudly in marketing — it's the anti-Grammarly stance.
- Zero-retention API configuration; no training on user data. (Validate the current provider terms before stating this publicly — see §8.)
- Sensitive-app blocklist (password managers, banking apps) — never capture from them.
- Context capture respects the redaction pass in §4.5.
- History is opt-in and local only (encrypted SQLite).
- BYO API key option for privacy-conscious users (also the Phase 0/1 default per §4.5); later, local-model option (MLX/Ollama) as a "Private mode" tier.

## 6. Design spec — "familiar shape, sharper substance"

v1 of this spec used a centered, dark-glass sci-fi card. Rejected after review (`mockup.html` v1) — it broke the core interaction: the card wasn't anchored to the selection, so it didn't feel like it was *responding to what you just did*. `mockup.html` v2 fixed positioning; a follow-up review found the diff view, action affordances, and context disclosure were still cosmetic rather than functional. `mockup.html` v3 (current) fixes those — see the changelog in §10.

### Design language

- **Positioning**: the card is a true popover, anchored to the selection's bounding rect — appears directly above the selected text, flips below if there's no room above, and **re-anchors continuously**: whenever streamed content changes the card's height, and on window resize/scroll, position is recomputed rather than set once. Dismisses on click-outside or Esc.
- **Form**: ~430 pt wide light card (white / `NSVisualEffectView` .windowBackground in light, dark-elevated surface in Dark Mode), 14 pt corner radius, small pointer triangle connecting it to the selection, soft shadow.
- **Accent, not costume**: a single teal accent bar + accent color carries the "AI" identity. No gradients, no glow, no animated borders.
- **Type**: SF Pro; body 13.5 pt, generous line height. Diff inline in the paragraph flow: deletions dimmed grey strikethrough, insertions on a pale teal highlight — and this diff is a real word-level diff between the original selection and the model's returned text (§4.5), not a hand-authored highlight of insertions only.
- **Motion**: card fades/slides in from the selection (6px, 140ms). Streaming reveals the computed diff token-by-token with a blinking caret.
- **States**: skeleton (thinking) → streaming (**all actions and tabs disabled/dimmed during this state — not just visually inert but actually non-interactive**, matching §4.5's failure-state discipline) → complete (actions live, Accept enabled only once a result exists) → applied (brief teal flash on the source text, then popover closes) → error (inline, one line, retry action, selection preserved).

### Card anatomy (top to bottom, matches `mockup.html`)

1. Header: badge + product name + usage line (free-tier count — see §8 on treating the number itself as unvalidated) + close ×.
2. **Context disclosure line** — states exactly what was used, e.g. "Using 2 visible Teams messages above as context" or "No thread context available in Notes — using only your selected text." This is the literal implementation of Promise B's disclosure requirement in §3, not a decorative footer.
3. Accent bar + heading naming what the rewrite did, + the diff body.
4. Action row: **Accept** (primary, disabled until a rewrite exists) · **Revise with AI** (opens the one-off free-text instruction row inline — this is a real input now, not a non-functional label) · Regenerate (↻, same tone) · Copy.
5. Tab row: Improve · Concise · Friendly · Expand — switching regenerates instantly. (The earlier "✦ Custom…" tab was removed — Revise with AI is the one real entry point for custom instructions, so there's no longer a second, non-functional path to the same thing.)

### Menu-bar presence

Tiny monochrome glyph; menu = pause toggle, per-app enable/disable, settings, history. No dock icon.

## 7. Roadmap

| Phase | Duration | Deliverable |
|---|---|---|
| 0 — POC | 3–4 wks | **Recommended clean scope (adopted from review):** one signed menu-bar app supporting selection capture, Haiku rewrite, and safe paste-back in **Notes and Teams only**. Context is limited to the selected draft in both apps — Teams thread context (Promise B) is a **separate, explicitly-flagged experiment** with its own visible disclosure, not bundled into the core deliverable. Exit criteria: passes the test matrix below in both apps. |
| 1 — MVP | 6–8 wks | Full card UI (v3 interaction model), local diffing, tone tabs, **one-off "Revise with AI" free-text instructions** (not persistent — see scope note below), onboarding + permission flow, BYO API key, notarized DMG. Expand app coverage only after each app passes its own test matrix. |
| 2 — Polish | 4–6 wks | **Persistent** custom instructions/preferences, history, personal style learning ("sound like me" from accepted rewrites), Sparkle updates, landing page, beta via direct distribution. |
| 3 — Grow | ongoing | Pricing (validate figures first — see §8), proxy backend + subscription billing, local model mode, then Windows (biggest lift — rebuild capture layer on UIA). |

**Scope note on custom instructions (resolves a v1.0 contradiction):** Phase 1 ships one-off "Revise with AI" — type an instruction, get a rewrite, it's not saved. Phase 2 adds persistence: saved instruction presets, learning from accepted rewrites, and history. These are explicitly different features now, not the same line item appearing in two phases with different implications.

### Phase 0 test matrix (replaces "reliable round-trip" with measurable criteria)

For **each** target app (Notes, Teams) run 20 consecutive attempts covering a mix of short (<1 sentence), medium (paragraph), and multi-line selections, and record:

| Criterion | Target |
|---|---|
| Successful selection capture (AX or clipboard fallback) | ≥ 90% (18/20) |
| Successful replacement landing in the correct field | ≥ 90% (18/20) |
| Clipboard restored to pre-transaction state when it should be, and correctly *not* restored when the user's clipboard changed mid-transaction | 100% — this is a correctness bar, not a success-rate bar |
| Zero instances of paste landing in the wrong app/field | 100% |
| Explicit fallback or error state shown on every failure (no silent no-ops) | 100% |

Phase 0 does not exit until both apps clear this table. Teams is the harder case (Electron) and is the priority signal — if Teams clears the bar, the fallback chain is validated for the rest of the Electron-app universe.

## 8. Costs & risks — figures here are directional, not validated

The numbers below are useful for thinking about order of magnitude, not for a launch or investor document. Before using them externally: confirm current Anthropic API pricing (it changes), confirm the actual zero-retention/no-training terms available at the account tier you'll use, and re-check the competitor pricing cited in §2 (subscription prices move).

- **API cost (unvalidated estimate)**: a rewrite is roughly 1K input / 300 output tokens → likely well under $0.01 on Haiku at current list pricing, but *verify this against the live pricing page before committing to a subscription price*. A back-of-envelope heavy-user estimate (50 rewrites/day) suggested ~$3–5/mo — again, treat as a planning input, not a committed unit economics figure.
- **Risk — AX flakiness in Electron/web apps**: mitigated by the clipboard-transaction tier in §4.2 (Grammarly and WordWand both ship an equivalent fallback), and now gated by the Phase 0 test matrix instead of an informal "it seems to work" check.
- **Risk — Apple Intelligence sherlocking**: Apple's Writing Tools are generic and context-blind; the moat is thread context (where validated per-app) + one-off/persistent custom instructions + cross-app consistency. Move fast on that moat, but only claim it where §3's Promise B has actually been validated for a given app.
- **Risk — trust**: an app with Accessibility access reading on-screen text needs a spotless privacy story from day one (§5), including honest disclosure of exactly what context was read (§3, §6) rather than implying more than was actually captured.
- **Risk — clipboard corruption**: without the transaction discipline in §4.2, a naive save/restore can destroy rich content or clobber a user's unrelated copy. This was unspecified in v1.0 and is now the single most detailed section of the plan for a reason.
- **Learning curve**: if Swift is new to you, Phase 0 is deliberately small and scoped to two apps. Claude Code can carry most of the implementation.

## 9. First concrete steps

1. Build the Phase 0 spike exactly as scoped in §7: signed menu-bar app, `⌥Space` hotkey (with conflict handling), AX selection read + clipboard fallback (with the transaction safety in §4.2), Claude call, local diff, paste-back — Notes and Teams only, no thread-context reading yet.
2. Run the Phase 0 test matrix (§7) yourself, logging actual pass/fail counts per criterion — don't eyeball it.
3. Only after both apps clear the matrix, start the Teams thread-context experiment as its own separately-flagged feature with its own disclosure UI (already mocked in `mockup.html` v3's app-switcher).
4. Validate the cost/pricing figures in §8 against live API pricing before any external-facing pricing decision.

## 10. Changelog from v1.0 (what a review caught and why it mattered)

| Issue in v1.0 | Fix in v1.1 |
|---|---|
| MVP treated universal selection-replace and cross-app thread context as one solved feature | Split into Promise A (reliable core) and Promise B (opt-in, per-app, disclosed) — §3 |
| Phase 0 exit criterion was "reliable round-trip," unmeasurable | Replaced with a numeric test matrix — §7 |
| Clipboard fallback was described as "save clipboard → paste → restore" with no edge-case handling | Full transaction design: all pasteboard types, change-count guard, timeout, focus re-verification — §4.2 |
| Free-text instructions listed under both Phase 1 and Phase 2 with different implied meanings | Explicitly split: Phase 1 = one-off, Phase 2 = persistent — §7 |
| No mention of hotkey conflicts, API key storage, who computes the diff, context limits/redaction, or failure states | Added §4.5 covering all five |
| Pricing/cost figures presented as settled | Reframed as directional estimates requiring validation before external use — §8 |
| Mockup diff view only rendered insertions | `mockup.html` v3 computes a real word-level diff (LCS) between original and rewritten text — real deletions included |
| "Revise with AI," Copy, and Custom were visually present but non-functional; Revise just reran the same rewrite | Revise with AI now opens a real instruction input routed through `computeCustomText`; redundant Custom tab removed; Copy calls the clipboard API |
| Actions stayed clickable during streaming, contradicting the stated state model | `.streaming` class now disables all actions/tabs via CSS + JS guards until the rewrite completes |
| Context label implied a broad read regardless of app | Card now shows an explicit context line that differs by app, demonstrated via the Teams/Notes switcher in the mockup |
| Card position was set once per open, not re-anchored as content streamed in or the window changed | `positionCard()` is now called on every content update plus on resize/scroll |

---

*Sources: [Grammarly Mac support docs](https://support.grammarly.com/hc/en-us/articles/10139846131213), [Grammarly desktop guide](https://support.grammarly.com/hc/en-us/articles/4412816078349-Grammarly-for-Windows-and-Grammarly-for-Mac-user-guide), [AX selected-text technique](https://macdevelopers.wordpress.com/2014/02/05/how-to-get-selected-text-and-its-coordinates-from-any-system-wide-application-using-accessibility-api/), [Electron AX selection bug](https://github.com/electron/electron/issues/36337), [Wordwand](https://wordwand.co/blog/best-ai-writing-mac), [RewriteBar](https://rewritebar.com/articles/writing-apps-for-mac), [Setapp: Grammarly alternatives](https://setapp.com/app-reviews/grammarly-alternatives), [Apple Intelligence vs Grammarly](https://medium.com/macoclock/apple-intelligence-writing-tools-vs-grammarly-a-comprehensive-comparison-between-the-best-ai-b515b96f7d68)*
