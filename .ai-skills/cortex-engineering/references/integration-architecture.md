# Integration Architecture Reference

Authoritative reference for Cortex **external integrations**: admin setup, credentials, health/tests, field discovery, mappings, and safety boundaries. Use this before changing backend integration services, integration endpoints, or `IntegrationsPage.tsx`.

---

## 1. Purpose

Cortex integrations are **governed, read-only / reference-first intake paths**. They connect external context to Cortex **without** making external systems the source of truth for routing, ownership, or approvals.

**Key framing**

- Cortex does **not** blindly sync provider systems into routing decisions.
- Provider data flows through: **controlled setup** → **credential handling** → **health checks** → **field discovery (live or planning)** → **admin-approved field/board mappings** → **explicit Cortex ticket / context flows** (where supported).
- **Deterministic Cortex rules** evaluate **canonical Cortex fields and persisted ticket state** only—not raw provider payloads—for routing and governance.

If a task proposes “sync Jira into routing” or “map ServiceNow priority directly to owner,” stop and align with this document and product owners first.

---

## 2. Current Provider Maturity

### SharePoint

**Status:** Supported **read-only** path.

**Capabilities**

- Provider-aware connection setup and validation profiles
- Microsoft **Graph / app-registration** oriented configuration
- **Live** list column field discovery where the implementation and URL/site context support it
- Read-only **sync** of external work items (where configured)
- **Field mappings** and **board mappings**
- **External items** review UI
- **Ticket creation from external item** where the product flow exists (explicit admin/user action—not silent automation)
- **Health** and **Test connection** with **live Graph validation** when fully configured, otherwise **local** checks and `TestUnavailable`-style outcomes

### Jira

**Status:** **Setup-ready**, not **live-sync-ready**.

**Capabilities**

- Provider-specific setup fields (e.g. base URL, project key, issue type, optional JQL-related fields)
- **Credential storage** via dedicated credential API (encrypted)
- **Credential audit** activity events
- **Local / metadata-safe** health and test readiness (no live Jira API validation in current product intent)
- **Static / common** field **planning** guidance in admin UX
- **Field mapping** UX with planning rows (advisory)

**Not implemented (do not assume without an explicit epic)**

- Live Jira API validation from Integrations test
- Live Jira field discovery
- Jira sync / continuous import
- Jira ticket **writes** or bidirectional updates

### ServiceNow

**Status:** **Setup-ready**, not **live-sync-ready**.

**Capabilities**

- Provider-specific setup fields (e.g. instance URL, table, optional category/query-related fields)
- **Credential storage** via dedicated credential API (encrypted)
- **Credential audit** activity events
- **Local / metadata-safe** health and test readiness (no live ServiceNow API validation in current product intent)
- **Static / common** field **planning** guidance
- **Field mapping** planning UX

**Not implemented (do not assume without an explicit epic)**

- Live ServiceNow API validation from Integrations test
- Live ServiceNow field discovery
- ServiceNow sync
- ServiceNow record **writes** or bidirectional updates

### SAP Reference

**Status:** **Metadata / catalog-only**.

**Capabilities**

- Stored SAP reference metadata surfaced in Cortex
- **SAP Reference Catalog** administration under Configuration (not a live ERP socket)
- **Advisory** SAP context in ticket Evidence / governance surfaces where the product exposes it

**Not implemented**

- Live SAP connection from Integrations
- SAP “work item” sync in the SharePoint/Jira sense
- SAP writes back to SAP systems

---

## 3. Security Rules (Non-Negotiable)

- **Secrets are never returned** to the frontend in any DTO or API response body for normal admin flows.
- **Secrets are not** stored in `ProviderSettings` / public connection JSON blobs—use the **credential** model and endpoints only.
- Secrets are submitted and rotated only through **dedicated credential endpoints** (see §4).
- At-rest secret protection uses **ASP.NET Core Data Protection** (or equivalent) for encrypted credential storage today; treat **credential store** as an abstraction **swappable** (e.g. Key Vault) in future without leaking semantics into business logic.
- **Audit / activity metadata** must **never** contain secret values, raw tokens, or decrypted payloads.
- **Logs** must **never** include credential payloads or secrets.
- Frontend **must not** render stored secrets; **credential inputs clear** after successful save where the UI implements that contract.
- Integration admin routes require **elevated / admin** authorization (same posture as other configuration surfaces).

---

## 4. Credential Lifecycle

**Endpoints** (under `/api/integrations`, elevated access):

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/connections/{connectionId}/credentials/status` | Non-secret status: configured labels, timestamps, auth mode |
| `PUT` | `/connections/{connectionId}/credentials` | Configure or rotate secrets (body keys only; values never echoed back) |
| `DELETE` | `/connections/{connectionId}/credentials` | Clear stored credentials |

**Audit / activity types** (safe metadata only):

- `CredentialConfigured`
- `CredentialRotated`
- `CredentialCleared`

**Safe metadata examples:** connection id, connection display name, provider, auth mode, `credentialConfigured`, **names/labels** of secret fields configured—**never** values.

---

## 5. Connection Health and Test Action

**Endpoints**

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/connections/{connectionId}/health` | Current derived health DTO for UI badges and readiness |
| `POST` | `/connections/{connectionId}/test` | Run a **safe** test; update last-test fields; record activity |

**Health status enum (typical)**

- `NotConfigured` — required non-secret settings missing or invalid
- `MissingCredentials` — profile expects credentials that are not satisfied
- `NotTested` — not yet exercised via test (or equivalent)
- `Healthy` — checks passed (provider-dependent meaning)
- `NeedsAttention` — failure or upstream error worth remediation
- `TestUnavailable` — **local** or **not available** path; see provider behavior

**Provider behavior (intent)**

- **SharePoint:** May run **live Microsoft Graph** validation when app + tenant context supports it; otherwise **local** validation with `TestUnavailable` / local test mode messaging.
- **Jira / ServiceNow:** **Local readiness** validation only for now; **no** live provider call as part of “success” path—copy should be honest in UI and responses.
- **SAP Reference:** **Metadata-only / reference-safe** test outcome—no live SAP.

**Activity event**

- `ConnectionTested`

**Activity metadata constraints**

Include only **safe** fields: e.g. success flag, test mode, health status, credential satisfied flag, missing/invalid **setting keys**, missing **credential field keys**, high-level message. **No** secrets, **no** raw exception strings from upstream that could leak tokens or stack details to clients—sanitize for admin display.

---

## 6. Field Discovery and Mapping Profiles

### Safe flow (required mental model)

```text
Provider field
  → discovered OR planning field metadata (admin-visible, non-secret)
  → admin-approved field mapping profile
  → Cortex canonical field / advisory context
  → existing Cortex rules evaluate canonical fields / ticket state only
```

### Forbidden flow

```text
Provider field → direct routing / owner / approval mutation
```

External fields **must not** short-circuit governance. Routing and approvals remain governed by Cortex rules and explicit workflows—not by unmapped provider columns.

### Current implementation snapshot

| Provider | Discovery mode | Mapping validation / notes |
|----------|----------------|----------------------------|
| **SharePoint** | **Live** list column discovery where URL/list/Graph path supports | May **reject** unknown mapped columns when **discovered column set is non-empty** and list URL parses; if discovery returns empty or URL missing, validation may be skipped to avoid blocking legacy/test setups |
| **Jira** | **Static planning** guidance only | No live discovery; planning DTOs are advisory |
| **ServiceNow** | **Static planning** guidance only | Same as Jira |
| **SAP Reference** | **N/A** for work-item field mapping | Catalog / metadata path—not normal external work-item field mapping source |

**Read-only overview API** (for admin UX): e.g. `GET /api/integrations/sources/{sourceId}/fields` returns discovery mode, status message, current mappings, and planning fields where applicable—**no** raw provider payloads unless already **sanitized** by existing DTOs.

---

## 7. Integration UI Principles

**Intended admin flow**

1. Create **connection** (provider-specific settings)
2. **Configure credentials** (when applicable)
3. **Test connection** (safe test; understand live vs local)
4. **Sources** — register lists/projects/tables (provider-dependent); **SAP Reference** redirects messaging to catalog, not fake “live SAP sources”
5. **Map fields** and **boards**
6. **Review external items** (where applicable)
7. **Review activity** / audit trail

**Provider maturity copy (enterprise-safe)**

- **SharePoint:** Supported **read-only** path; discovery and sync where implemented.
- **Jira:** Setup and credential storage available; **live** validation and sync **not** enabled yet.
- **ServiceNow:** Same as Jira.
- **SAP Reference:** **Stored metadata only**; **not** a live SAP connection.

The **Provider readiness** matrix in the Integrations UI summarizes columns such as setup, credentials, health test, field discovery, sync, and current status—keep backend behavior consistent with that honest framing.

---

## 8. Things Not To Build Accidentally

Do **not** implement the following **without explicit product task approval** and security review:

- Full **Jira** or **ServiceNow** sync engines
- **Bidirectional** provider writes
- **Live SAP connector** for work-item style sync
- **OAuth consent** flows surfaced as “done” when not specced
- **Key Vault** migration **as a drive-by** (requires infra + secret rotation story)
- **AI-assisted** field mapping that auto-writes mappings without admin confirmation
- **RAG** over external systems as part of integration core
- **Automatic routing** or **silent ticket creation** driven straight from external fields
- Any path that sends **secrets** to logs, activity JSON, or the browser

---

## 9. Future Roadmap (Indicative)

Likely **progressive** enhancements—**not** commitments:

1. Real SharePoint connection validation with **test data** / hardened Graph diagnostics
2. Jira **read-only discovery v1** (fields/items as specced)
3. ServiceNow **read-only discovery v1**
4. Provider field discovery **hardening** (schemas, transforms, governance)
5. **AI-assisted mapping suggestions** (always admin-approved)
6. **Key Vault** (or managed secret store) **behind** credential abstraction
7. **OAuth** / enterprise app flows where required by customers
8. **RAG** or doc-assisted mapping over **approved** corpora—not silent exfiltration

---

## 10. Verification

- This file is **documentation-only**. Changes here **must not** alter runtime behavior.
- After edits, run: `git diff -- .ai-skills`
- If any **application source** changed in the same change set unintentionally, run `dotnet test`, `npm run build`, and `npm run lint` as appropriate.

---

## Related References

- `agent-operating-rules.md` — global agent constraints; integrations must not violate routing/approval rules.
- `routing-rules.md` — canonical routing; external fields do not bypass.
- `enterprise-hardening.md` — security posture alignment.
- `integrations.md` — short pointer and historical stub; **this document** is the source of truth for **current** integration architecture.
