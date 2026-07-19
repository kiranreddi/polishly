# Polishly SEO Plan (agent brief)

Use this as the source of truth when improving discoverability for Polishly (macOS app + marketing site + GitHub). Prefer shipping small, measurable changes over big rewrites.

**Product one-liner (keep consistent everywhere):**  
Free, open-source macOS menu-bar app for explicitly-invoked, in-place AI rewriting — bring your own API key (Groq/Cerebras free tiers supported). No subscription. No background scanning.

**Primary surfaces**
- Marketing site: `website/` (static; build copies to `website/dist/client/`)
- GitHub: https://github.com/kiranreddi/polishly
- README: `/README.md`
- Releases/DMG: GitHub Releases `v1.0.0` + `website/assets/releases/Polishly-1.0.0.dmg`

**Canonical brand terms:** Polishly, macOS, rewrite, in-place, bring your own API key, open source, menu bar, Accessibility, Groq, Cerebras.

---

## 0. Goals & constraints

### Goals (priority order)
1. Rank for high-intent queries about free / BYO-key Mac rewriting tools.
2. Convert visitors → DMG download → Accessibility grant → first rewrite.
3. Earn GitHub stars / referrals from “Grammarly alternative” and “free AI rewrite Mac” searches without overclaiming.

### Non-goals / hard rules
- Do **not** claim App Store availability.
- Do **not** claim always-on grammar checking (anti-positioning vs Grammarly).
- Do **not** promise universal Teams/Slack thread context (Promise B is off).
- Do **not** invent pricing/retention claims for AI providers; link out and say tiers change.
- Keep copy honest: Polishly is free; model usage may be free via Groq/Cerebras tiers with rate limits.
- Avoid purple/glow AI clichés in new page visuals; match existing teal + dark site tokens.

### Success metrics (track when analytics exist)
- Organic sessions → `#download` click → DMG download
- Queries: impressions/CTR for target keywords
- GitHub referral traffic from site / README

---

## 1. Keyword map (use these phrases intentionally)

### Primary (homepage title/H1/meta)
| Intent | Target phrases |
|---|---|
| Core product | `macOS AI rewrite`, `Mac text rewrite app`, `in-place AI writing Mac` |
| Free / BYOK | `free AI rewrite Mac`, `bring your own API key writing app`, `open source Grammarly alternative` |
| Workflow | `select text hotkey rewrite Mac`, `menu bar AI writing assistant` |

### Secondary (sections / FAQ / future pages)
| Intent | Target phrases |
|---|---|
| Competitor | `Grammarly alternative Mac free`, `Apple Intelligence Writing Tools alternative` |
| Provider | `Groq API writing app`, `Cerebras API Mac app`, `use Groq for rewriting` |
| Privacy | `no background scanning AI Mac`, `Accessibility AI rewrite privacy` |
| Apps | `rewrite text in Notes Mail Slack Teams Mac` |

### Avoid stuffing / weak keywords
- Generic “best AI writer 2026” listicle bait without proof
- “ChatGPT for Mac” unless page truly compares workflows
- “App Store” / “notarized” as ranking claims until stapled notarization is confirmed

---

## 2. Technical SEO checklist (do first)

Current gap: site has title + description only — no OG/Twitter, canonical, sitemap, robots, or structured data.

### A. On every indexable HTML page (`website/index.html` and any new pages)
- [ ] Unique `<title>` ≤ ~60 chars, include **Polishly** + primary intent
- [ ] Unique meta description ≤ ~155 chars, include free + BYOK + macOS
- [ ] Canonical absolute URL once production domain is known
- [ ] Open Graph: `og:title`, `og:description`, `og:type=website`, `og:url`, `og:image` (1200×630)
- [ ] Twitter card: `summary_large_image` + same image
- [ ] Meaningful favicon + apple-touch-icon already partly present — keep
- [ ] One H1 only; logical H2/H3 hierarchy matching nav sections
- [ ] Image `alt` text (brand icon currently empty `alt=""` — fix to “Polishly app icon”)
- [ ] Internal links: logo → home, CTAs → `#download`, footer → Download/GitHub/Docs

### B. Site files to add
- [ ] `website/robots.txt` — allow `/`, point to sitemap
- [ ] `website/sitemap.xml` — homepage (+ future pages)
- [ ] `website/assets/og-cover.png` — generate from existing hero/card art (`docs/images/`)
- [ ] Ensure `scripts/build.mjs` copies robots/sitemap/og into `dist/client/`

### C. Performance / CWV
- [ ] Keep CSS/JS small (already static)
- [ ] Compress OG/PNG assets; lazy-load below-fold images if added
- [ ] No blocking third-party scripts until analytics is deliberate
- [ ] Mobile layout already exists — re-check Download section after changes

### D. Indexing ops (human / agent with access)
- [ ] Confirm production URL (Sites / custom domain)
- [ ] Google Search Console + Bing Webmaster verify
- [ ] Submit sitemap
- [ ] Request indexing for homepage after major meta/content ship

---

## 3. On-page content plan (homepage)

Keep single-page architecture for v1; strengthen sections rather than exploding into many thin pages.

### Hero
- H1 should include searchable meaning, not only brand poetry.  
  Example direction: **“Free AI text rewrite for Mac — in place, with your own API key.”**  
  Keep a short brand line under it if needed.
- Primary CTA: Download for macOS  
- Secondary: How it works / GitHub  
- Supporting line: free Groq key path

### Download section (`#download`) — conversion hub
- Keep version, DMG filename, macOS 14+, Accessibility steps
- Add FAQ-style mini copy answering: “Is it safe?”, “Do I need to pay?”, “Does Gatekeeper warn?”
- Link README free-key guide

### Features / Compare / FAQ
- Expand FAQ answers with keyword-rich but natural Qs:
  - “Is Polishly a free Grammarly alternative?”
  - “Can I use Groq for free with Polishly?”
  - “Does Polishly work in Slack and Teams?”
  - “Is my text sent in the background?”
- Compare table: keep factual; add last-reviewed date note

### Structured data (JSON-LD)
Add to homepage:
1. `SoftwareApplication` — name Polishly, OS macOS, price 0, category Productivity, downloadUrl, offers
2. `FAQPage` — mirror visible FAQ Q&As (must match on-page text)
3. `Organization` / `WebSite` once domain is stable

---

## 4. Content expansion (phase 2 pages)

Only add pages that can rank and convert. Suggested IA:

| Path | Intent | Notes |
|---|---|---|
| `/` | Brand + download | Current site |
| `/download` (or keep `#download`) | Navigational | Optional dedicated page if analytics show scroll drop-off |
| `/grammarly-alternative` | Competitor | Honest comparison; CTA to Download |
| `/groq-setup` | Free tier how-to | Step-by-step from README; screenshots |
| `/privacy` | Trust | Explicit hotkey-only sending; Keychain; no backend |
| `/changelog` | Product updates | Link Releases |

**Blog (optional, later):** 4–6 cornerstone posts max to start — not a spam blog.
1. “How to rewrite text system-wide on Mac without Grammarly”
2. “Use Groq free API with a Mac menu-bar rewrite app”
3. “Why Accessibility permissions matter for Mac writing tools”
4. “Apple Intelligence Writing Tools vs BYO-key rewrite apps”

Each post: one primary keyword, 1 screenshot, CTA to Download, internal links to FAQ/Compare.

---

## 5. GitHub + README SEO (developer discovery)

GitHub ranks for product names and “awesome” style queries.

- [ ] Repo description: include `macOS`, `AI rewrite`, `open source`, `BYOK`
- [ ] Topics: `macos`, `swift`, `ai`, `writing-tools`, `openai`, `groq`, `menu-bar`, `accessibility`, `open-source`
- [ ] README already strong — ensure first 10 lines answer what/why/install/download
- [ ] Pin Releases; keep DMG asset name stable (`Polishly-x.y.z.dmg`)
- [ ] Social preview: set GitHub repo social image if available
- [ ] Avoid leaking internal plan docs in public-facing README (already removed PLAN/mockup links)

---

## 6. Distribution / off-site SEO (light, high leverage)

Not classic on-page SEO, but agents should not ignore:

1. **Launch posts** (Product Hunt / Hacker News / Reddit r/macapps, r/selfhosted-adjacent carefully): same one-liner + DMG + “free with Groq”
2. **Directory listings:** AlternativeTo, SaaSHub, Slack/Mac app lists — consistent name + screenshots
3. **Backlinks:** one solid privacy writeup + one Groq/Cerebras tutorial beats 50 thin posts
4. **YouTube/Short (optional):** 30–45s “select → hotkey → Accept” screen recording
5. **Do not** buy spammy links or mass-syndicate AI articles

---

## 7. Analytics & measurement (when ready)

- [ ] Privacy-friendly analytics (Plausible/Fathom/Cloudflare) — disclose in Privacy page
- [ ] Events: `cta_download_click`, `dmg_download`, `github_click`, `faq_open`
- [ ] UTM on launch posts: `utm_source`, `utm_medium=social`, `utm_campaign=launch`
- [ ] Monthly keyword review: drop pages that don’t get impressions after 8–12 weeks

---

## 8. Suggested agent execution order

### Sprint A — foundation (1 sitting)
1. Add OG/Twitter tags + og image + canonical placeholder/domain
2. Fix empty image alts
3. Add `robots.txt` + `sitemap.xml`; update `build.mjs`
4. Add `SoftwareApplication` + `FAQPage` JSON-LD
5. Tune title/H1/description toward primary keywords without killing brand voice
6. Rebuild website dist; deploy

### Sprint B — conversion copy
1. Strengthen Download section SEO copy
2. Expand FAQ with 3–5 target questions
3. Align GitHub topics/description with keyword map
4. Confirm notarization status language accuracy on Download page

### Sprint C — expansion
1. Ship `/groq-setup` and `/privacy` (or equivalent)
2. One competitor page (`grammarly-alternative`)
3. Search Console setup + sitemap submit

### Definition of done for any SEO PR
- [ ] No keyword stuffing / no false claims
- [ ] Mobile layout OK
- [ ] Build still produces `dist/client` correctly
- [ ] Title/description unique
- [ ] Download CTA still works (site asset and/or GitHub Releases)
- [ ] Screenshots/alts present where new images added

---

## 9. Copy snippets agents may reuse

**Title options**
- `Polishly — Free AI Rewrite for Mac (BYO API Key)`
- `Polishly | Open-source in-place AI writing for macOS`

**Meta description**
- `Polishly is a free, open-source macOS menu-bar app that rewrites selected text in place. Bring your own API key (Groq, Cerebras, OpenAI, Anthropic). No subscription. No background scanning.`

**OG image text (if designing)**
- Polishly  
- Rewrite anywhere on your Mac  
- Free · Open source · Your API key

---

## 10. Open questions (resolve before claiming)

1. Final production domain? (needed for canonical/sitemap/OG URLs)
2. Is DMG notarization stapled yet? (affects Gatekeeper messaging)
3. Will the GitHub repo stay public? (required for Releases SEO/downloads)
4. Any analytics vendor preference / privacy constraints?

When domain is known, replace all `https://EXAMPLE.com` placeholders in meta/canonical/sitemap in one PR.
