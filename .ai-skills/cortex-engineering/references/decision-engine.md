# Decision Engine Reference

## 1. Purpose

The Decision Engine chooses the best owner for each ticket using deterministic policy-first logic augmented by advisory AI context. It exists to keep assignment outcomes consistent, explainable, and operationally balanced. In Cortex tier context, it is a core cross-tier engine that supports deterministic routing today and controlled evolution later.

## 2. How It Works (High Level)

Inputs include normalized ticket attributes, rule eligibility/match data, current owner workload, and bounded learning adjustments. The engine outputs a selected owner, ranked alternatives, and explicit decision reasons with confidence context. Core logic applies a deterministic order (rules -> eligibility -> workload -> approved learning adjustment), then uses AI only to enrich explanation or highlight ambiguity. It depends on routing rules, eligibility checks, workload metrics, and override history.

## 3. Signals / Inputs

- Ticket signals: priority, board/department scope, SLA posture, ticket attributes used by rules.
- User signals: explicit overrides, historical manual correction patterns, assignee availability indicators.
- System signals: rule match strength, candidate pool size, eligibility flags, workload distribution, tie-break metadata.
- AI signals: advisory tie-context explanations and weak-signal ambiguity notes.

## 4. Output / Behavior

The engine produces one selected owner, alternatives, confidence context, and transparent rationale. Outputs are presented as deterministic reasoning first, with AI commentary clearly marked as supporting context. The result influences assignment, rebalance consideration, and audit trace quality.

## 5. Constraints (NON-NEGOTIABLE)

- Deterministic first: rules and eligibility always lead.
- Must remain explainable and auditable.
- Must not allow AI to directly pick owners.
- Must not bypass routing rules, approvals, or policy constraints.
- Must not mutate unrelated fields as a side effect of owner selection.
- Must not expose sensitive internal data.
- If a system already exists, extend it - do not recreate it.

## 6. UX Language Rules

Use:
- "Cortex selected this owner based on routing rules and current workload."
- "Based on current signals, this was the strongest eligible match."

Avoid:
- "The model chose this owner."
- "AI overrode routing."

## 7. Tier Alignment

Belongs to core deterministic decisioning across tiers with AI augmentation bounded to advisory roles. It must not overlap with Tier 11 autonomous intervention behavior or Tier 12 orchestration authority. Tier expansion must preserve deterministic precedence.

## 8. Extension Guidelines (CRITICAL)

- Safe extensions: new deterministic signals, refined tie-breakers, calibrated learning adjustments, improved reasoning text.
- Extend existing routing and workload services instead of creating alternate decision paths.
- Add new signals behind observable, testable adapters.
- Must not introduce opaque scoring that hides decision lineage.
- Must not promote AI from advisory to authority without explicit spec and governance approval.

## 9. Common Failure Modes

- No candidate pool after eligibility filtering.
- Conflicting rules with unclear precedence handling.
- Over-reliance on AI commentary when deterministic signals are weak.
- Silent drift from workload weighting changes without audit coverage.
- Override feedback loops that unintentionally bias future decisions.

## 10. Example Scenario

Sample input: ticket matches two routing rules, both candidates are eligible, Owner A has significantly lower active high-priority load, and learning adjustment slightly favors Owner B based on prior overrides.

Expected output: select Owner A with alternatives listing Owner B; rationale prioritizes stronger deterministic workload balance despite minor learning signal.

Reasoning: rule and eligibility narrow candidates, workload differential decides, learning adjustment remains bounded and non-authoritative.