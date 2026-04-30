# Fresh Install Validation Results

## Date / Environment

| Field | Value |
|-------|--------|
| **Date** | **2026-04-29** |
| **Mode tested** | **Local validation with fresh database** — SQL and apps brought up consistent with **`docs/pilot-setup.md`** (Docker SQL tooling where applicable; full three-service **docker-compose** stack not asserted for this checklist run). |
| **Validator** | **Adam Hooper** |

---

## Summary

Core **fresh-install / pilot-path** workflows **passed** on an empty database after documentation and env templates were added for Tier 12. One **reporting correctness** issue surfaced in **Configuration → Routing → Rule Health** (match counts vs routing decisions); it was **fixed in backend aggregation** (distinct **`TicketRoutingDecision`** ticket IDs for **`MatchCount`**, cache bypass for rule-health effectiveness reads — see §Bug Found). Overall: the stack behaves as intended for onboarding a first routing rule and first ticket approvals.

This document is **not** a production go-live certification; see **`docs/README.md`** for pilot documentation scope.

---

## Validation Results

| Area | Result | Notes |
|------|--------|--------|
| Fresh empty Tickets page | Pass | Loads cleanly |
| User directory | Pass | Loads |
| First admin / active user | Pass | Setup works |
| Configuration page | Pass | Loads |
| Role definitions empty state | Pass | Clean |
| Routing empty state | Pass | Clean |
| Routing starter | Pass | Visible where expected |
| First routing rule creation | Pass | Can create |
| Ticket creation | Pass | First ticket created |
| Approval Queue | Pass | Usable |
| Approve path | Pass | Owners applied |
| Return for Detail path | Pass | Works |
| Reject path | Pass | Works |
| Owner assignment | Pass | Syniti Owner and Business Owner on approval |
| Rule Health | Pass after fix | Match count mismatch fixed; aligns with **`TicketRoutingDecision`** before terminal outcomes |
| Reports / Intake Learning empty state | Not verified | Left for follow-up session |
| Comments / attachments | Not verified | Left for follow-up session |

*(Result meanings: **Pass** — exercised successfully; **Pass after fix** — passed after corrective change documented below; **Not verified** — not exercised in this run; **Follow-up** — queued for later.)*

---

## Bug Found During Validation

### Rule Health match count mismatch

| Topic | Detail |
|--------|---------|
| **Observed behavior** | **Last matched** showed a populated timestamp while **Matches / Outcomes** showed **0 / 0** after a first routing rule matched and owners were assigned. |
| **Expected behavior** | **Matches** reflects **distinct tickets** with a **`TicketRoutingDecision`** for that rule (e.g. **1 / 0** when one ticket matched and **no** terminal **`TicketOutcome`** yet). **`Outcomes`** (sample for SLA/terminal learning) can remain **0** until completions exist. |
| **Root cause** | **`CortexLearningService.GetRoutingRuleEffectivenessAsync`** **cached** an early **`TotalDecisions`** snapshot (**0**) before routing decisions existed; **Rule Health** used that cached **`TotalDecisions`** for **`MatchCount`** while **Last matched** was computed directly from **`TicketRoutingDecision`**, producing inconsistent UI. |
| **Fix summary** | **`MatchCount`** is derived from **distinct **`TicketId`** for the rule’s **`MatchedRuleId`** in **`TicketRoutingDecisions`**; effectiveness for rule health bypasses stale cache (**`bypassCache`**) when loading outcome metrics alongside that cohort. *(See codebase change in Tier 12 fix — not recreated here.)* |
| **Verification result** | After fix: **Matches / Outcomes** shows **1 / 0** for one non-terminal matched decision; **Last matched** populated; **health** appropriately **Insufficient data** until volumes/thresholds met. |

---

## Current Pilot Readiness Status

- Cortex **starts** from a **fresh / empty** environment (with correct env and Auth0 as in **[pilot-setup.md](pilot-setup.md)**).
- **Admin / user setup** can be **completed**.
- **Routing** can be **configured** (first rule, starter UX).
- **First ticket workflow** can be **executed** (create → queue → approve / return / reject).
- **Approval routing assigns owners** (Syniti and Business paths observed).
- **Rule Health** reflects **routing decisions before terminal outcomes** exist (**match vs outcome counts** distinguished).

Pilot messaging remains bounded: this validates **engineering / demo readiness**, not hardened multi-tenant production operations.

---

## Remaining Optional Checks

Worth scheduling before broader pilot scale or demos that stress alternate topologies:

- **Full docker-compose mode** — all three Compose services (**`docker-compose.yml`**) end-to-end.
- **OpenAI disabled / degraded mode** — confirm bounded behavior with **empty** **`OpenAI__ApiKey`**.
- **Azure SignalR / hosted realtime** — multi-instance path vs **in-process SignalR**.
- **Auth0 — second account** pending **access approval** (unknown / inactive governance paths).
- **Attachment upload / delete** storage and edge cases.
- **Reports** with **larger historical** ticket/outcome volumes.
- **Rebalance** with enough **workload / decision** data to surface recommendations.

---

## References

- [pilot-setup.md](pilot-setup.md) — pilot setup runbook  
- [fresh-install-validation-checklist.md](fresh-install-validation-checklist.md) — validation checklist followed for this Tier 12 cycle  
- [README.md](../README.md) — product overview and **Operations / Pilot Setup**  
- [.env.example](../.env.example) — placeholder environment variable names (**do not commit secrets**)  

Documentation index for the Tier 12 packet: [docs/README.md](README.md).

---

*Fresh install validation results — Tier 12 Task 6 (documentation only).*
