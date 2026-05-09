# Cortex Skill Loader

When working in this repository, ALWAYS load:

- cortex-engineering

---

## Reference Documents (REQUIRED CONTEXT)

The cortex-engineering skill includes reference documents located at:

.ai-skills/cortex-engineering/references/

Agents MUST treat these files as required context when relevant.

Core references include:

- integration-architecture.md
- ui-language.md
- ui-standards.md
- decision-engine.md
- routing-rules.md
- rebalance.md
- metrics.md
- demo-mode.md
- enterprise-hardening.md
- api-contracts.md
- data-model.md
- test-strategy.md
- playbooks.md

If a referenced document exists on disk:

- DO NOT ignore it
- DO NOT claim it is missing
- APPLY its rules when performing tasks

---

## Non-Negotiable System Rules

This project enforces:

- Deterministic routing (rules > AI)
- Advisory-only AI (never auto-mutate canonical fields)
- Localized UI updates (no full refreshes unless unavoidable)
- Enterprise SaaS UX standards (clear, operational, executive-readable)
- Decision-first UX (every surface explains what, why, and impact)

---

## Decision System Alignment (CRITICAL)

All changes MUST align with:

- decision-engine.md
- routing-rules.md
- rebalance.md
- metrics.md

Cortex is a **decision system**, not a CRUD application.

Agents must preserve:

- explainability
- confidence signaling
- workload-aware logic
- safe execution (no instability, no ping-pong)

---

## UI Language Enforcement (CRITICAL)

All user-facing text MUST follow:

- ui-language.md
- ui-standards.md

### Never use:

- system-facing wording (e.g. "unavailable", "invalid", "no data")
- ambiguous phrasing
- developer/internal terminology

### Always prefer:

- decision clarity ("No clear owner identified")
- signal-based reasoning ("Insufficient routing signals")
- action guidance ("Needs clarification")
- impact framing ("Reduces SLA risk")

UI must feel:

- intentional
- trustworthy
- operationally clear

---

## Expected Agent Behavior

- Do NOT re-architect without explicit instruction
- Prefer surgical changes over broad refactors
- Preserve existing behavior unless task explicitly changes it
- Maintain demo-readiness at all times
- Do NOT introduce regressions in routing, rebalance, or AI guardrails

---

## Output Requirements

Every task response MUST include:

1. Files changed
2. What changed
3. Why it changed
4. How to verify
5. Risks / follow-ups

---

## Anti-Patterns (STRICTLY DISALLOWED)

- Reintroducing full-page reloads where localized updates exist
- Exposing internal system state in UI
- Breaking deterministic routing with AI behavior
- Weakening rebalance safety (ping-pong, instability)
- Adding features that dilute the core decision flow

---

## Definition of Done

A change is ONLY complete if:

- It preserves system rules
- It aligns with decision-engine behavior
- It uses correct UI language
- It improves clarity OR maintains clarity
- It passes build + does not degrade demo flow
