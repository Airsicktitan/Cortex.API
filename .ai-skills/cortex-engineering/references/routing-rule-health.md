# Routing Rule Health Reference

## 1. Purpose

Routing Rule Health monitors whether routing policy is functioning as intended and producing reliable candidate pools. It exists to detect degraded rule quality before it causes assignment errors or operational drift. In tier context, it supports deterministic routing integrity across all tiers.

## 2. How It Works (High Level)

The system evaluates routing outcomes against rule eligibility, match quality, and candidate generation behavior. It outputs health status, detected anomalies, and remediation recommendations. Core logic compares expected rule behavior to observed selection/candidate results and flags gaps such as empty pools or rule drop-offs. Dependencies include routing rules, eligibility service, decision engine traces, and assignment telemetry.

## 3. Signals / Inputs

- Ticket signals: rule-evaluable attributes (priority, board, requester department/role), assignment outcomes.
- User signals: override frequency by route path, manual reassignment patterns.
- System signals: eligibility flags, department mapping, rule match criteria, candidate pool size, rule execution errors.
- AI signals: advisory anomaly summaries and clustering of recurring routing mismatches.

## 4. Output / Behavior

The system produces routing health status (healthy/degraded/failing), issue list, and prioritized fixes. It presents findings with rule-level evidence and impact context. It influences rule tuning, fallback strategy review, and operational triage for routing incidents.

## 5. Constraints (NON-NEGOTIABLE)

- Must not mutate routing rules automatically.
- Must remain read-only and advisory unless explicitly governed by approved maintenance flow.
- Must preserve deterministic policy precedence in all analyses.
- Must not suppress or hide rule failures.
- Must not expose sensitive identities or internal-only debug data.
- If a system already exists, extend it - do not recreate it.

## 6. UX Language Rules

Use:
- "Cortex has identified routing rule health degradation."
- "Based on current signals, candidate generation is unstable."

Avoid:
- "AI fixed routing automatically."
- "Model replaced routing logic."

## 7. Tier Alignment

Belongs to deterministic routing assurance and supports all tiers. It must not overlap with decision authority from the decision engine, and it must not introduce Tier 11+ autonomous correction behavior.

## 8. Extension Guidelines (CRITICAL)

- Safe extensions: additional health metrics, richer anomaly diagnostics, better alert prioritization.
- Add detectors through existing routing telemetry and rule-evaluation pipelines.
- Keep checks deterministic and reproducible.
- Must not introduce hidden fallback routing that bypasses official rules.
- Must not convert advisory health checks into autonomous rule mutation.

## 9. Common Failure Modes

- Role-based filtering incorrectly excludes valid owners.
- Empty candidate pool due to strict or stale eligibility constraints.
- Silent rule failures where rules fail evaluation without surfaced errors.
- Department mapping drift causing persistent mismatches.
- Health status remains green despite repeated manual overrides.

## 10. Example Scenario

Sample input: multiple tickets from one department produce no candidates after role filters, while rule criteria appear matched.

Expected output: `Degraded` health with findings ("role-based filtering conflict", "empty candidate pool", "silent rule evaluation error risk"), plus recommendation to validate eligibility mapping and rule criteria ordering.

Reasoning: matching rule criteria without viable candidates indicates health failure in eligibility or filter configuration, not ticket randomness.
