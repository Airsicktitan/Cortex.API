# UI Standards (Enterprise SaaS)

## Purpose

Cortex UI must feel:

- fast
- clear
- intentional
- operational (not decorative)

---

## Golden Rule

Every UI element must answer:

"What action does this enable?"

If it doesn't → remove or simplify it.

---

## Language Rules

### GOOD

- "Needs Attention"
- "Ready for Review"
- "Ownership Risk"
- "Expected Impact"
- "Blocked: Owner no longer eligible"

---

### BAD

- "Data Error"
- "Invalid State"
- "AI Result"
- Raw IDs (userId, ticketId)
- Developer terms in user views

---

## Display Rules

### Always show:

- Display names (NOT IDs)
- Human-readable statuses
- Clear ownership labels

---

## Panel Design

Every panel should follow:

### 1. Title (clear purpose)
### 2. Key signal (what matters)
### 3. Supporting context
### 4. Action (if applicable)

---

## Example (Good Panel)

Cortex Decision

- Selected Owner: Sarah
- Reason: Matches Finance routing rule, lower workload
- Alternatives:
  - John → Higher workload
  - Mike → Weaker rule match

---

## Example (Bad Panel)

- Owner: ID_1234
- Reason: Computed score = 0.82

---

## Visual Hierarchy

Priority order:

1. Critical signals (SLA risk, blocked, overdue)
2. Ownership clarity
3. Actions
4. Secondary info

---

## Color Usage

- Red → risk / breach / blocked
- Yellow → warning / attention
- Green → healthy / ready
- Neutral → informational

DO NOT:
- use color without meaning
- rely on color alone (must include text)

---

## Interaction Rules

### MUST:

- preserve scroll position
- preserve modal state
- update locally when possible

---

### SHOULD NOT:

- reload entire board
- reset UI unnecessarily
- flicker on updates

---

## Real-Time Behavior

SignalR updates should:

- update only affected components
- not disrupt user interaction
- not trigger full rerender

---

## Forms / Modals

### Requirements:

- prefill existing data
- persist user edits where possible
- avoid losing work on refresh

---

## AI UI Rules

AI sections must:

- clearly label as advisory
- show structured output
- NOT feel like "magic"

Good:
"Suggested Priority: High  
Reason: Missing required system error details"

Bad:
"AI thinks this is important"

---

## Rebalance UI Rules

Each suggestion must:

- read like a sentence
- be immediately understandable

Good:
"Move Ticket A from John → Sarah  
Reason: Reduces SLA risk and balances workload"

Bad:
"Reassignment candidate with improved score"

---

## Performance Perception

UI must feel:

- instant (<100ms interactions)
- responsive (no blocking spinners unless necessary)

---

## Anti-Patterns (DO NOT DO)

- ❌ raw backend data exposed to users
- ❌ inconsistent terminology
- ❌ multiple panels saying the same thing differently
- ❌ unclear actions
- ❌ hidden logic

---

## Executive Readiness Rule

A non-technical stakeholder should be able to:

- understand what’s happening
- understand why it’s happening
- know what to do next

within 5 seconds.