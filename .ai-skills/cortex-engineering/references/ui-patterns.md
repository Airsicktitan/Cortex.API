# Cortex UI Patterns

## 1. Purpose

Provide concrete UI patterns for Cortex.

This file defines:

- how components should look
- how information should be structured
- how UI should be implemented consistently

This complements:

- UI Standards (tone, wording)
- UI Architecture (structure, layout)

---

## 2. Cortex Panel Pattern

### Structure

Cortex panel must always follow:

Cortex  
[Tabs]

Active Tab Content

---

### Tabs

Tabs must be:

- horizontal
- clearly labeled
- minimal
- consistent across screens

Example:

Decision | Risk | Insight | Intake

---

## 3. Decision Tab Pattern

### Purpose

Answer:

What should happen next?

---

### Layout

Decision

Recommended Owner  
Sarah Johnson

Selected Because

- Matches Finance routing rule
- Lower workload than alternatives

Impact  
Risk reduced from High to Moderate

Alternatives

- John → Higher workload
- Mike → Weaker rule match

---

### Rules

- Show 2–3 reasons max
- Do not show raw scores
- Do not show internal calculations
- Keep concise

---

## 4. Review Tab Pattern (Approval Only)

### Purpose

Answer:

Can I approve this?

---

### Layout

Review

Readiness  
Small gaps remain

Missing Details

- Expected result
- Business impact

AI Summary  
User cannot complete workflow due to missing authorization

Visual Evidence  
Screenshot reviewed by Cortex

---

### Rules

- Readiness must be clearly visible
- Missing details must be actionable
- No long paragraphs

---

## 5. Risk Tab Pattern

### Purpose

Answer:

What could go wrong?

---

### Layout

Risk

SLA Status  
At Risk

Signals

- High priority
- Near SLA deadline
- Owner workload high

Recommended Attention  
Review before end of day

---

### Rules

- Use signal-style bullets
- Prioritize urgency
- Avoid explanation-heavy text

---

## 6. Insight Tab Pattern

### Purpose

Answer:

What happened before?

---

### Layout

Insight

Similar Tickets

- #123 → Approval delay pattern
- #118 → Same board and priority

Why This Matters  
Similar tickets required follow-up

---

### Rules

- Show only relevant matches
- Avoid listing too many tickets
- Keep explanation short

---

## 7. Intake Tab Pattern

### Purpose

Answer:

Is this ready?

---

### Layout

Intake

Readiness  
Needs detail

Missing Details

- Expected result
- Business impact

---

### Rules

- Must be actionable
- Must be clear to non-technical users

---

## 8. Rebalance Pattern

### Purpose

Answer:

Should ownership change?

---

### Layout

Rebalance

Move Ticket  
John → Sarah

Reason  
Balances workload and reduces SLA risk

---

### Rules

- Must read like a sentence
- Must be immediately understandable
- Avoid technical wording

---

## 9. Tab Behavior Pattern

### Rules

- Only ONE tab visible at a time
- No stacked panels
- Tabs switch instantly
- Preserve modal state
- Default tab depends on mode

---

### Default Tabs

Ticket Mode  
Default: Insight

Approval Mode  
Default: Review

---

## 10. Signal Pattern

### Purpose

Convert logic into readable UI.

---

### Example

Instead of:

- High priority increases urgency
- SLA threshold nearing

Use:

High Priority  
SLA At Risk

---

### Rules

- Short
- Scannable
- No explanation required

---

## 11. Empty State Pattern

### Purpose

Handle missing data cleanly.

---

### Example

No insight available yet

No risk detected

---

### Rules

- Never show blank panels
- Never show raw null data
- Always provide context

---

## 12. Action Pattern

### Purpose

Make actions clear and intentional.

---

### Example

Approve  
Request More Info  
Reassign

---

### Rules

- Actions must be obvious
- Do not hide primary actions
- Do not overload with options

---

## 13. Anti-Patterns

DO NOT:

- stack AI panels vertically
- show raw backend data
- overload tabs with content
- duplicate signals across tabs
- show Decision to non-approvers
- use long paragraphs

---

## 14. Success Criteria

UI is correct when:

- user understands next action instantly
- approver can make a decision without scrolling
- tabs feel clean and focused
- no panel feels overwhelming

---

## Final Rule

If a UI pattern adds clarity → keep it  
If it adds complexity → remove it
