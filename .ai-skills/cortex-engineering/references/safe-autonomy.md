# Safe Autonomy Reference

## 1. Purpose

Safe Autonomy defines how Cortex can assist with decision flow while preserving explicit human control and policy compliance. It exists to prevent unsafe automated behavior and tier overreach. In tier context, it governs bounded autonomy signals that can inform recommendations and controlled actions only where approved.

## 2. How It Works (High Level)

The system ingests confidence and rule-quality signals, evaluates whether guardrails are satisfied, and determines whether behavior stays advisory or proceeds through approved autonomy gates. It outputs autonomy posture (advisory/hold/escalate), with reasons and required approvals. Core logic is gate-based: weak confidence or weak policy match downgrades to advisory. Dependencies include decision engine confidence, routing rule validation, override ledger, and workload telemetry.

## 3. Signals / Inputs

- Ticket signals: priority, SLA pressure, assignment state, recency of ticket updates.
- User signals: approval status, override history, owner confirmation/acknowledgment.
- System signals: rule match strength, workload pressure, policy gate state, escalation channel status.
- AI signals: confidence score, ambiguity indicators, uncertainty annotations.

## 4. Output / Behavior

The system produces a safe autonomy posture plus explanation: proceed within scope, request approval, or remain advisory. It presents decisions in explicit policy language with confidence context and gate status. It influences whether recommendations stay informational or enter controlled approval workflows.

## 5. Constraints (NON-NEGOTIABLE)

- No autonomous action without approved gate satisfaction.
- Weak confidence must not trigger autonomous behavior.
- Must respect deterministic routing and policy controls first.
- Must remain explainable and auditable.
- Must not bypass approval chains or override records.
- Must not expose sensitive data in autonomy reasoning.
- If a system already exists, extend it - do not recreate it.

## 6. UX Language Rules

Use:
- "Cortex has identified a safe recommendation path."
- "Based on current signals, approval is required before action."

Avoid:
- "AI is taking over this workflow."
- "Model confidence is enough to auto-act."

## 7. Tier Alignment

Belongs to cross-tier autonomy governance with strict respect for tier boundaries. It must not overlap with Tier 9 predictive guidance logic or replace Tier 11 controlled intervention policy definitions. It provides guardrails, not business routing policy.

## 8. Extension Guidelines (CRITICAL)

- Safe extensions: add confidence features, improve gate diagnostics, refine downgrade behavior.
- Extend existing gate evaluators and approval services only.
- Keep new rules deterministic and testable.
- Must not add direct autonomous side effects outside approved workflows.
- Must not allow AI confidence to supersede explicit policy gates.

## 9. Common Failure Modes

- Ping-pong assignments caused by unstable autonomy decisions.
- Acting on weak confidence due to threshold misconfiguration.
- Bypassing approval gates when workload pressure is high.
- Missing override history causing repeat unsafe recommendations.
- Ambiguous status messages that hide why autonomy was blocked.

## 10. Example Scenario

Sample input: ticket has high urgency, medium confidence recommendation, weak rule match, and a history of recent manual overrides.

Expected output: autonomy posture set to "advisory/approval-required" with reasons ("weak rule match", "override history risk"), plus explicit next step to request human approval.

Reasoning: safety gates reject autonomous progression because confidence and policy strength are insufficient.
