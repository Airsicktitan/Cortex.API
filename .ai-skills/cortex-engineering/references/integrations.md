# Integrations

## Purpose

Cortex connects to external systems through **governed, read-only / reference-first** integration paths. The platform remains the **decision layer**; external tools supply **context and intake**, not automatic routing authority.

**Authoritative architecture, security boundaries, and provider maturity:** see **[integration-architecture.md](./integration-architecture.md)**. Treat that document as source of truth for agents and engineers.

---

## Strategy (Current Product)

- **Augment** existing tools (SharePoint lists, future Jira/ServiceNow read paths)—do **not** replace Cortex routing or approvals with provider fields.
- External data flows: **setup → credentials → health/test → (discovery or planning) → mappings → explicit Cortex actions**.
- **Never** wire provider columns directly to routing, owner assignment, or approval state without mapping through canonical Cortex fields and existing rules.

---

## Target Integrations (Maturity)

Summarized here; details in `integration-architecture.md`:

| Provider | Summary |
|----------|---------|
| **SharePoint** | Supported **read-only** path; Graph; discovery/sync where implemented |
| **Jira** | **Setup-ready**; credentials + planning; **not** live sync/discovery |
| **ServiceNow** | **Setup-ready**; same boundaries as Jira |
| **SAP Reference** | **Metadata/catalog-only**; not a live SAP connector |

---

## Integration Pattern

`Intake → normalize (via mappings) → Cortex ticket/context → governed workflows`

Not: `External system → auto-owner / auto-routing / silent tickets`.

---

## Requirements

- Mapping layer (**external → Cortex canonical**), admin-governed
- Idempotent, auditable operations where sync exists
- Safe error handling; **no secret leakage** in logs or DTOs

---

## Anti-Patterns

- Tightly coupling routing logic to one provider’s schema
- Duplicating external data without a clear governance story
- Breaking core Cortex logic or **bypassing approvals** for integration convenience
- Implementing **full sync**, **bidirectional writes**, or **live discovery** without an explicit approved epic (see `integration-architecture.md` §8)

---

## Golden Rule

**Cortex remains the decision layer.** Integrations feed **context**; they do not override **routing rules**, **ownership decisions**, or **approvals**.

