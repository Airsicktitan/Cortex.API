# Routing Decision Clarity

## Purpose

Define how routing decisions must be explained in Cortex UI.

This ensures:

- trust
- transparency
- fast decision-making

---

## Core Rule

Every routing decision must be understandable in under 5 seconds.

---

## Decision Structure (Required)

All decisions must follow:

Recommended Owner

Selected because:

- reason 1
- reason 2
- reason 3

Alternatives considered:

- Owner A → reason not selected
- Owner B → reason not selected

---

## Selected Because Rules

- Maximum 3 reasons
- Must be bullet points
- Must be human-readable
- Must NOT be paragraphs

---

## Allowed Signals

Only include meaningful signals:

- Routing rule match
- Department match
- Priority match
- Workload comparison
- Availability

---

## Disallowed Signals

Do NOT show:

- raw scores
- confidence percentages
- internal weights
- rule IDs
- system calculations

---

## Language Rules

Bad:

- "Decision Rationale"
- "Confidence Score"
- "Computed Result"
- "No better eligible alternative identified"

Good:

- "Selected because"
- "Higher workload"
- "Weaker match"
- "No routing rule matched"

---

## Alternatives Rules

Alternatives must ALWAYS include reasoning.

Bad:

- John
- Mike

Good:

- John → Higher workload
- Mike → Weaker match

---

## No-Rule Scenario

If no routing rule matched:

Show:

No routing rule matched

Assigned based on:

- workload
- availability

---

## Fallback Clarity

Never leave a decision unexplained.

There must ALWAYS be a visible reason.

---

## UI Rules

- No paragraphs
- No system language
- No duplicate reasoning
- No hidden logic

---

## Anti-Patterns

DO NOT:

- show "0% confidence"
- show raw scoring
- hide why alternatives lost
- explain internal engine logic
- mix multiple concepts in one line

---

## Success Criteria

A user can:

- understand who was selected
- understand why in one glance
- understand why others were not selected

without asking questions

---

## Final Rule

Decisions must feel justified, not calculated.
