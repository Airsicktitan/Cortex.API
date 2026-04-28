# AI Security Audit Reference

## 1. Purpose

AI Security Audit defines how Cortex validates AI-related data safety across generation, transport, logging, and rendering paths. It exists to prevent data leakage, unsafe output exposure, and trust regressions. In tier context, it is a cross-tier safety control that every AI-adjacent capability must satisfy.

## 2. How It Works (High Level)

The audit consumes endpoint behavior, DTO mappings, logging behavior, exception handling, and AI output handling patterns. It outputs classified findings, risk severity, and hardening recommendations. Core logic follows a deterministic checklist across AI outputs, APIs, logs, configuration, and UI rendering, verifying that sanitization and safe contracts are enforced. Dependencies include sanitizer utilities, DTO mapping layers, exception middleware, and API contract standards.

## 3. Signals / Inputs

- Ticket signals: none directly required for core audit checks.
- User signals: endpoint access patterns, debug usage patterns, support-reported exposure incidents.
- System signals: API schemas, DTO/entity mappings, exception middleware behavior, logging configuration.
- AI signals: raw/processed AI output paths, prompt/response handling boundaries, sanitizer coverage.

## 4. Output / Behavior

The system produces an audit report with risk levels, affected areas, and practical mitigation actions. It presents findings as business-safe, implementation-focused guidance with severity labels. It influences release readiness, remediation priority, and acceptance criteria for AI feature changes.

## 5. Constraints (NON-NEGOTIABLE)

- Must treat AI as advisory and enforce deterministic safety controls.
- Must never allow unsanitized AI output to reach user-facing responses.
- Must not expose secrets, stack traces, internal schema details, or hidden identifiers.
- Must enforce DTO-based response contracts.
- Must remain auditable with clear evidence per finding.
- If a system already exists, extend it - do not recreate it.

## 6. UX Language Rules

Use:
- "Cortex has identified a potential exposure risk."
- "Based on current signals, output sanitization is required."

Avoid:
- "The model leaked internals."
- "AI returned raw system details by design."

## 7. Tier Alignment

Belongs to cross-tier security and safety governance. It must not overlap with routing/decision policy logic and must not be weakened by tier-specific autonomy changes.

## 8. Extension Guidelines (CRITICAL)

- Add new endpoint checks through the existing audit checklist and sanitizer validation flow.
- All new AI endpoints must pass sanitizer validation before release.
- All responses must use DTO contracts; no direct entity responses.
- New findings should map to severity taxonomy and evidence standards.
- Must not introduce direct response passthrough for raw AI output.

## 9. Common Failure Modes

- Logging raw AI output that may contain sensitive or internal details.
- Leaking exception messages directly to clients.
- Exposing database definitions, table names, or schema internals in responses.
- Returning entities instead of DTOs.
- Missing sanitizer coverage for new AI endpoints.

## 10. Example Scenario

Sample input: a new AI summary endpoint returns generated text directly and logs full prompt/response payloads during debug mode.

Expected output: high-risk findings for "raw AI logging" and "unsanitized response path", with required remediation to pass output through sanitizer and DTO wrapper, and to redact logs.

Reasoning: both transport and observability layers expose unsafe data, violating non-negotiable safety constraints.
