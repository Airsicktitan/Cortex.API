# Cortex Tooltip Style Guide

Short, implementation-oriented rules for tooltip copy and placement. Read this before writing a new tooltip, and before touching an existing one.

## The one component

Use `CortexTooltip` from `./ui/Tooltip`. It wraps `@radix-ui/react-tooltip` with the project's defaults (100ms delay, top placement, shared light/dark surface). Do **not** use raw HTML `title=""`, do **not** reach for Radix primitives directly, and do **not** introduce a new tooltip pattern.

```tsx
<CortexTooltip content="Pure insight: this value isn't enforced, just informational.">
  <button aria-label="About SLA Risk">?</button>
</CortexTooltip>
```

If you find native `title=""` anywhere in the codebase, migrate it.

## When to use a tooltip

Tooltip when:
- the control's visible label + nearby helper text already carries the primary meaning, and you want to add one useful extra detail (definition, unit, threshold, why it matters),
- you need to explain an icon-only button,
- you're clarifying a metric the viewer might misread (e.g. "this is a duration proxy, not human work hours").

Not a tooltip when:
- the information is required to use the control safely — that's helper text, not a hover,
- the content is longer than ~2 short sentences — move it into a help link or inline explanation,
- it would just repeat the visible label.

## Content rules

**Length.** One sentence is the target. Two at most, and only when the second adds a real caveat ("Proxy for effort — not human work hours"). Kill filler.

**Tone.** Direct, operational, product-aware. Same voice as Cortex Decision and Cortex Impact copy: cause → effect, not marketing prose.

**Sentence style.**
- Full sentences with a period. No trailing exclamation points.
- Start with a capital letter.
- Third person or imperative. Never "we" or "our."
- No multi-line tooltips. If you're tempted to use `\n`, the content belongs elsewhere.

**Say what it means, not how it's built.** "Sum of lifecycle durations across tickets in this group" is fine. "Current implementation remains bounded by existing recommendation and guardrail settings" is implementation leak — cut it.

**Advisory vs enforced.** If a value is advisory (not enforced by the system), say so: "Advisory only — not enforced." Users rely on this distinction when making decisions.

**Numbers and units.** Spell out units the first time ("minutes", "tokens") — don't assume.

## Words and phrases to avoid

- "helps manage", "helps improve", "helps users" — vague.
- "opportunity", "might help", "may be useful" — wishy-washy.
- "Allows AI to …", "Enables or disables …" — redundant verb stacks. Prefer "AI recommends …" / "On = …".
- Internal names that aren't visible in the UI (`IsTriageEnabled`, `synitiOwner`, service class names).
- Parenthetical caveats longer than the main sentence.

## Format rules

- Plain text only inside `content`. No headings, bold, lists, or line breaks — the surface isn't sized for layout.
- Max practical length: ~160 characters. Past that, content should live in helper text, a tooltip-adjacent "Learn more" link, or an inline explanation block.
- Trigger element must be focusable and have an accessible label (`aria-label` on icon buttons, real text on buttons/spans that carry content).

## Labels, helper text, and tooltips — how they divide

| Element     | Purpose                                   | Example                                                    |
| ----------- | ----------------------------------------- | ---------------------------------------------------------- |
| Label       | Name the control                          | "Confidence Threshold"                                     |
| Helper text | Primary guidance, always visible          | "Cortex discards recommendations below this confidence."   |
| Tooltip     | One supplemental detail on hover          | "0 = accept everything. 1 = accept only perfect matches."  |

If removing the tooltip would break comprehension, the explanation belongs in helper text instead.

## Voice examples — before / after

**Redundant with label**
- Before: "Enables or disables AI-generated status recommendations."
- After:  "On — Cortex AI can recommend status transitions. Off — it won't."

**Too long / implementation leak**
- Before: "AI can operate with minimal user confirmation, within configured guardrails. Current implementation remains bounded by existing recommendation and guardrail settings."
- After:  "AI acts on its own within the configured guardrails. Humans only confirm exceptions."

**Vague**
- Before: "Comfortably inside the SLA window."
- After:  "On track to resolve before the SLA deadline."

**Multi-line native title**
- Before: `title="About SLA Risk\n\nSLA Risk is a workload-based signal derived from the current open, at-risk, and breached ticket load across the assigned owners. It helps indicate whether this assignment may need closer monitoring. It is not a guaranteed SLA outcome."`
- After (as `CortexTooltip`): "Advisory signal based on the assigned owner's current open, at-risk, and breached tickets. Not an SLA prediction."

## Accessibility

- Every tooltip trigger must be reachable by keyboard (Radix handles the rest).
- Icon-only triggers need `aria-label` — the tooltip body is not a substitute.
- Don't hide critical information behind hover alone. Mobile and assistive tech users will miss it.

## Adding a new tooltip — checklist

1. Is the visible label + helper text enough on its own? If yes, stop.
2. Is your copy one sentence, under ~160 characters, plain prose?
3. Does it say what the value *means*, not how it's computed internally?
4. Is "advisory vs enforced" clear if it matters?
5. Is the trigger keyboard-focusable with an accessible name?
6. Are you using `CortexTooltip`? (Not `title=`, not Radix primitives directly.)

If any answer is no, rewrite before merging.
