# Polishly

**Select text anywhere on your Mac. Press one hotkey. Get a real, in-place rewrite.**

Polishly is a free, open-source macOS menu-bar app for explicitly-invoked AI rewriting. No background scanning, no subscription, no vendor lock-in — you bring your own API key (or skip the network entirely with local demo mode), and your text goes straight from your Mac to the provider you chose.

```
Select text  →  ⌃⌥Space  →  card appears with a rewrite + inline diff  →  Accept
```

## Why Polishly

- **$0 forever.** The app itself is free and open source. You only ever pay your own provider's per-token usage — no subscription, no markup, no middleman.
- **Bring your own key.** OpenAI, Anthropic, Groq, or Cerebras — pick a provider, paste your key, done. Stored in the macOS Keychain, never in plaintext prefs.
- **No key? No problem.** Local demo mode runs entirely on-device with simple rule-based cleanup — zero network calls, zero cost, useful for trying the interaction model before connecting a real provider.
- **Explicitly invoked, not always-on.** Polishly never reads or sends anything until you press the hotkey. No underlining, no continuous scanning of what you type.
- **A real diff, not a leap of faith.** Every rewrite is shown as a word-level diff against your original selection before you accept it.
- **Works system-wide.** Notes, Mail, Slack, Teams, browsers — anywhere macOS Accessibility can read a selection.

## Install (build from source)

There's no signed release yet — build it yourself:

```sh
brew install xcodegen   # if you don't already have it
git clone git@github.com:kiranreddi/polishly.git
cd polishly
xcodegen generate
xcodebuild -project Polishly.xcodeproj -scheme Polishly -configuration Debug build
open ~/Library/Developer/Xcode/DerivedData/Polishly-*/Build/Products/Debug/Polishly.app
```

First launch asks for Accessibility access (System Settings → Privacy & Security → Accessibility) — required to read your selection and paste the rewrite back in. Polishly starts in local demo mode; connect a real provider any time from the menu-bar icon → Settings.

## Setting up a provider

Settings → **Rewrite Provider** → choose OpenAI, Anthropic, Groq, or Cerebras → paste your API key → **Save & Remember Key**. The model field is free text, so you can point it at any model string the provider supports (e.g. `gpt-4.1-mini`, `claude-haiku-4-5`, `llama-3.3-70b-versatile`, `gpt-oss-120b`).

Get a key from whichever provider you'd rather pay directly:
[OpenAI](https://platform.openai.com/api-keys) · [Anthropic](https://console.anthropic.com/) · [Groq](https://console.groq.com/keys) · [Cerebras](https://cloud.cerebras.ai/)

## Using it

1. Select text in any app.
2. Press **⌃⌥Space** (configurable in Settings).
3. Pick a tone tab — **Improve · Concise · Friendly · Expand** — or click **Revise with AI** and type any free-form instruction.
4. Review the inline diff, then **Accept** to replace the text in place, or **Copy**.

### Real examples

These are genuine, unedited outputs from Polishly's "Revise with AI" custom-instruction path, captured during testing — not cherry-picked marketing copy.

**Instruction: "translate this to Spanish"**
> Input: *I think we should push the meeting to next week.*
> Output: *Creo que deberíamos posponer la reunión para la próxima semana.*

**Instruction: "make this concise — cut it to two sentences"**
> Input: *Our Q3 numbers came in below target, mostly because of the delayed product launch. The engineering team hit a few last-minute bugs that pushed the release by three weeks. Marketing had already spent a chunk of the budget on the original launch date, so some of that spend is now sunk. We're revising the Q4 forecast to account for the slower start. I'd like to get the team together this week to talk through next steps.*
> Output: *Our Q3 results missed targets due to a delayed product launch caused by last-minute engineering bugs, with marketing budget partially sunk. We're revising the Q4 forecast accordingly and will meet this week to discuss next steps.*

**Instruction: "rewrite this as a bulleted list"**
> Input: *The new onboarding flow requires users to verify their email, set up two-factor authentication, and complete a short profile before they can access the dashboard. Early data shows about 40% of users drop off during the 2FA step...*
> Output:
> - Users must verify their email
> - Users must set up two-factor authentication
> - Users must complete a short profile
> - Access to the dashboard is granted after these steps
> - Approximately 40% of users drop off during the 2FA step
> - Simplifying the process could significantly improve user activation

**Instruction: "make it sound apologetic"**
> Input: *Sorry I missed your call, I was in a meeting all day.*
> Output: *I apologize for missing your call; I was tied up in a meeting all day.*

**Instruction: "convert this into numbered steps"**
> Input: *(a multi-paragraph customer email about a delayed furniture order with three resolution options)*
> Output:
> 1. Inform Ms. Rodriguez about the update on her custom furniture order number 48291 placed on March 3rd.
> 2. Explain the delay due to the walnut veneer batch failing quality inspection...
> 3. Present two alternatives: substitute a similar in-stock veneer, or wait ~5 weeks for an exact match.
> 4. Offer a partial refund regardless of the chosen option.
> 5. Request her preference or offer a phone call.

## How it's built

- Swift + SwiftUI, AppKit `NSPanel` for the floating card (non-activating, so focus stays in whatever app you're rewriting in).
- macOS Accessibility API for reading the selection and writing the rewrite back in place, with a clipboard-transaction fallback (full pasteboard snapshot, change-count guard, focus re-verification) for apps with flaky AX support.
- A local word-level diff engine — the model returns clean rewritten text only; Polishly computes the displayed diff itself.
- Streaming completions from whichever provider you've configured, with a per-request token budget that scales with input length so long selections and "expand" instructions aren't cut off mid-sentence.

See [`PLAN.md`](PLAN.md) for the full product/technical plan, including the market positioning research and design rationale, and [`mockup.html`](mockup.html) for the interaction-model reference the card UI is built from.

## Testing

```sh
xcodegen generate
xcodebuild -project Polishly.xcodeproj -scheme Polishly -configuration Debug test
```

`Tests/PolishlyTests/ReviseQualityTests.swift` is a diagnostic (non-CI) harness that exercises the real configured provider against 10 varied custom-instruction cases plus a long-output truncation stress test, and writes the results to disk for manual review. It reads whichever provider key you've already saved in Keychain — it never asks for or handles a key directly.

## Privacy

Polishly only sends text when you explicitly invoke the hotkey — never in the background, never continuously. In real-provider mode, your selection goes directly from your Mac to the provider you configured, using your key. Polishly itself has no backend and never sees your text. Sensitive fields (password managers, banking apps) are never captured from.

## Contributing

Issues and PRs welcome. This is a small, personal open-source project — no formal process, just be reasonable.

## License

[MIT](LICENSE)
