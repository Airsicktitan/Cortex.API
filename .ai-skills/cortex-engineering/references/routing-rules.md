# Routing Rules Reference

## 1. Purpose

Routing Rules define how Cortex deterministically selects the best eligible owner for a ticket. They exist to ensure assignment consistency, fairness, and explainability while respecting policy boundaries. In tier context, this is foundational deterministic logic used across all tiers.

## 2. How It Works (High Level)

The system ingests ticket routing attributes and evaluates rule eligibility/match strength in a strict precedence order. It outputs one selected owner plus ranked alternatives with reasoning. Core logic applies deterministic sorting and then workload-aware tie resolution, not random or opaque selection. Dependencies include eligibility flags, workload scoring, and decision explainability services.

Routing priority order:
1. RulePriority DESC
2. Weight DESC
3. Match Count DESC
4. Id ASC

## 3. Signals / Inputs

- Ticket signals: `BoardId`, `Priority`, `RequesterDepartment`, `RequesterRole`.
- User signals: explicit assignment overrides and override recency/history.
- System signals: owner eligibility flags (`IsSynitiOwnerEligible`, `IsBusinessOwnerEligible`), active ticket count, high/critical load, SLA risk, overdue/stale counts.
- AI signals: advisory explanation support only; no owner selection authority.

## 4. Output / Behavior

Each routing decision produces selected owner, alternatives (excluding selected), reasons for selection, reasons alternatives were rejected, and confidence score. Output is presented as deterministic rationale first, with full traceability for audit and user review. It influences ticket ownership assignment and downstream workload balance.

## 5. Constraints (NON-NEGOTIABLE)

- Owner must be eligible.
- Must respect `IsSynitiOwnerEligible` and `IsBusinessOwnerEligible`.
- Must include workload context in decisions.
- No random assignment.
- AI must not choose owners.
- No silent recomputation from unrelated UI actions.
- If a system already exists, extend it - do not recreate it.

## 6. UX Language Rules

Use:
- "Cortex selected this owner based on routing priority and workload."
- "Based on current signals, this was the strongest eligible match."

Avoid:
- "AI picked this owner."
- "The model rerouted this ticket."

## 7. Tier Alignment

Belongs to deterministic routing foundations across tiers. It must not overlap with Tier 11 autonomous intervention controls or Tier 12 orchestration authority. Higher tiers may consume routing outcomes but must not bypass routing rule precedence.

## 8. Extension Guidelines (CRITICAL)

- Safe extensions: add approved routing signals, tune deterministic weights, improve confidence explanation.
- Extend existing routing and eligibility services rather than creating parallel assignment paths.
- Preserve strict precedence ordering and auditability.
- Must not introduce opaque model-only owner selection.
- Must not bypass override persistence behavior.

## 9. Common Failure Modes

- Eligibility flags out of date, producing invalid candidates.
- Workload signals ignored, causing concentrated queue pressure.
- Conflicting rule matches without clear precedence application.
- Silent route recomputation after user interaction.
- Missing alternative rationale reducing trust.

## 10. Example Scenario

Sample input: ticket has `BoardId=IT`, `Priority=High`, `RequesterDepartment=Finance`, two matching rules, and two eligible owners where Owner B has lower high-priority load.

Expected output: Owner B selected, Owner A listed as alternative, confidence and explicit rationale tied to rule precedence plus workload difference.

Reasoning: deterministic rule ordering narrows candidates, workload input resolves tie, and output remains explainable.