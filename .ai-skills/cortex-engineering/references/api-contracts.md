# Decision Engine (How Cortex Thinks)

## Purpose

The Decision Engine defines how Cortex makes decisions end-to-end.

It unifies:

- AI Intake
- Routing
- Workload Evaluation
- Rebalance
- Execution

---

## Core Principle

Cortex is a **deterministic decision system with AI augmentation**

Rules > Workload > AI

---

## Decision Pipeline

### 1. Intake

Input:
- raw ticket data

Output:
- structured ticket

---

### 2. AI Analysis (Advisory Only)

Generates:
- summary
- suggested priority
- missing details
- risk signal

Rules:
- must use system vocabulary
- must NOT mutate system fields

---

### 3. Routing (Deterministic)

Uses:
- rules
- eligibility
- workload scoring

Outputs:
- selected owner
- alternatives
- reasoning

---

### 4. Evaluation Layer

Combines:

- rule match strength
- workload impact
- SLA risk
- optional AI signals (advisory)

Produces:
- decision confidence
- impact assessment

---

### 5. Rebalance Engine

Evaluates system-wide state:

- identifies imbalance
- proposes improvements
- validates candidates

---

### 6. Execution

Applies:

- explicit routing decisions
- explicit rebalance actions
- explicit user overrides

---

## Signal Weighting

Priority:

1. Rule match
2. Eligibility
3. Workload balance
4. SLA risk
5. AI signals (supporting only)

---

## Confidence Scoring

Confidence reflects:

- strength of rule match
- difference between candidates
- workload clarity

---

### Example

High:
- clear rule match + better workload

Low:
- weak rule + similar candidates

---

## Override Behavior

If user overrides:

- persist override
- mark decision as overridden
- retain original recommendation

---

## Explainability Structure

Every decision must include:

- selected owner
- why selected
- alternatives
- why alternatives rejected
- confidence
- impact

---

## Anti-Patterns

- ❌ AI selecting owners
- ❌ hidden decision logic
- ❌ recomputing silently
- ❌ non-deterministic outcomes

---

## Golden Rule

A human must be able to answer:

"Why did this happen?"

within 5 seconds.