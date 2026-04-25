# Rebalance Engine Rules

## Purpose

Rebalance exists to:

- reduce workload imbalance
- reduce SLA risk
- improve ownership clarity
- prevent ticket stagnation

It is NOT a suggestion engine — it is a **decision support system with execution fidelity**.

---

## Core Principle

### What is shown MUST equal what is executed

If the UI shows:
"Move Ticket A from John → Sarah"

Execution MUST:
- move to Sarah
- NOT recompute a different owner
- NOT silently re-evaluate a new winner

---

## Suggestion Requirements

Each rebalance suggestion MUST include:

### 1. Action

- Ticket name/id
- From owner
- To owner

---

### 2. Why (Required)

- workload imbalance
- SLA risk
- ownership mismatch
- rule alignment improvement

---

### 3. Expected Impact (Required)

For BOTH:

#### Source owner:
- workload reduced
- SLA pressure reduced

#### Target owner:
- workload increased (acceptable)
- better alignment / lower risk

---

### 4. Confidence

- High → clear improvement
- Medium → acceptable tradeoff
- Low → edge case / weak improvement

---

## Candidate Selection Rules

When generating suggestions:

- Only include **eligible owners**
- Respect:
  - IsSynitiOwnerEligible
  - IsBusinessOwnerEligible

---

## Workload Scoring Inputs

Must consider:

- active ticket count
- high/critical tickets
- SLA at-risk or breached
- overdue/stale tickets

---

## Blocked Suggestions

If a suggestion cannot be executed, it MUST be classified as:

### Stale
- ticket changed since suggestion generation

### Invalid
- owner no longer eligible

### Conflicted
- routing rules now disagree

---

## UI Rules

Suggestions must be split into:

### Actionable
→ can be executed immediately

### Blocked
→ must clearly show WHY they cannot be executed

---

## Execution Rules

When executing rebalance:

- Execute EXACT suggestions shown
- Validate eligibility at execution time
- If invalid:
  - mark as blocked
  - DO NOT silently reroute

---

## Anti-Patterns (DO NOT DO)

- ❌ Recompute new owner at click time
- ❌ Hide why a suggestion exists
- ❌ Show suggestions without impact explanation
- ❌ Mix valid and invalid suggestions without labeling
- ❌ Execute different result than UI displayed

---

## Performance Rules

- Avoid recomputing full dataset on every request
- Use cached snapshots when possible
- Avoid double evaluation (suggest + execute)

---

## Trust Rule (MOST IMPORTANT)

If the user loses trust in rebalance:

The feature is worse than not existing.

Everything must feel:

- predictable
- explainable
- consistent