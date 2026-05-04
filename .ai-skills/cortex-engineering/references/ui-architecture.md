# Cortex UI Architecture

## 1. Purpose

Define how Cortex UI is structured.

This enforces:

- where information lives
- how panels are organized
- how roles change the UI

This complements UI Standards (tone, wording, signals).

---

## 2. Core Layout Rule

Every Ticket Modal must follow:

Left Side:

- ticket data
- comments
- editable fields

Right Side:

- Cortex panel ONLY

No AI content should appear outside the Cortex panel.

---

## 3. Cortex Panel System

All AI functionality must be organized into a tabbed panel.

Never stack multiple AI panels vertically.

---

## 4. Tab System

Tabs must represent distinct responsibilities.

Each tab answers ONE question.

---

## 5. Mode-Based UI

Cortex operates in two modes:

### Ticket Mode (Non-Approver)

Tabs:

- Insight
- Risk
- Intake

Default:

Insight

Do NOT show:

- Decision
- Autonomy

---

### Approval Mode (Approver)

Tabs:

- Review
- Decision
- Risk
- Insight

Default:

Review

---

## 6. Tab Responsibilities

Each tab must be isolated.

### Review

Question:
Can I approve this?

Contains:

- readiness
- missing details
- triage summary
- screenshot evidence

---

### Decision

Question:
What should happen next?

Contains:

- recommended owner
- reasoning
- alternatives
- impact

Only visible to approvers.

---

### Risk

Question:
What could go wrong?

Contains:

- SLA risk
- workload risk
- follow-up risk

---

### Insight

Question:
What happened before?

Contains:

- similar tickets
- match reasoning
- learning signals

---

### Intake

Question:
Is this ready?

Contains:

- missing details
- clarity issues
- intake quality signals

---

## 7. No Duplication Rule

Information must exist in ONE tab only.

Do NOT repeat:

- reasoning
- signals
- summaries

Across multiple tabs.

---

## 8. Rendering Rules

- Only render active tab content
- Do NOT stack panels
- Do NOT preload unused tabs (unless required for data flow)
- Do NOT hide panels with CSS only

---

## 9. Role-Based Visibility

UI must adapt by role:

Non-Approver:

- no decision logic
- no system reasoning

Approver:

- full decision context
- full risk + insight

---

## 10. Data Ownership

Each panel owns its data:

- Decision panel → routing + decision engine
- Risk panel → SLA + workload
- Insight panel → memory + embeddings
- Review panel → triage + intake

No panel should fetch another panel’s data.

---

## 11. Interaction Model

- Tabs switch instantly (no reload)
- Modal state is preserved
- Data updates are localized
- No full modal refresh

---

## 12. Anti-Patterns

DO NOT:

- stack AI panels
- mix responsibilities in one tab
- show Decision to non-approvers
- duplicate signals across tabs
- render all tabs at once
- expose backend structures in UI

---

## 13. Success Criteria

UI is correct when:

- user understands the next step instantly
- approver can act without scrolling
- tabs feel clean and focused
- no panel feels overwhelming
- system feels intentional, not generated

---

## 14. Final Rule

Structure > Features

If a feature breaks UI clarity, it must be restructured before being added.
