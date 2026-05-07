# Syniti Knowledge Domain v1 (Cortex)

## Purpose

Reviewer-first, **advisory** terminology and governance context for Syniti / data migration work. Complements **SAP Reference Intelligence** without changing routing, owners, approvals, or ticket fields.

## Safety

- **Curated catalog only** — generic, reusable phrasing suitable for demos; no customer data, tickets, or internal URLs.
- **No live ingestion** — no SharePoint/Jira scraping, no external APIs, no secrets.
- **Source of truth** — `SynitiKnowledgeEntries` in the Cortex database, populated idempotently from `SynitiKnowledgeCuratedCatalog` at startup (`SynitiKnowledgeDevCatalogSeed.EnsureAsync`).

## Knowledge entry shape (conceptual)

| Concept | Storage |
|--------|---------|
| id | `SynitiKnowledgeEntry.Id` |
| term | `Term` |
| aliases | `Aliases` (semicolon-separated; match with same strength as term) |
| category | `SynitiKnowledgeCategory` enum (including Reconciliation, Readiness, FieldOwnership, LoadProcessing, Cutover, Support) |
| shortDescription | `ShortDefinition` |
| reviewerGuidance | `BusinessMeaning` (also projected as `ReviewerGuidance` on API) |
| suggestedChecks | `SuggestedReviewerChecks` (pipe-separated) |
| missingDetailsQuestions | `MissingContextQuestions` (pipe-separated) |
| relatedConcepts | `RelatedTerms` (UI: related concepts preview) |
| relatedSapContexts | _Future_: encode hints in `RelatedTerms` or a dedicated column — v1 relies on **Governance Summary** combining SAP + Syniti in the UI. |
| sensitivityLevel / sourceType / isSafeForDemo | v1: all seed rows are demo-safe manual catalog; no extra columns. |

## Matching rules (`SynitiKnowledgeDetector`)

- Scan **combined ticket + integration context** via `ReviewerTicketContextAssembler`.
- Per catalog row (ordered by longest term first): try **term**, then **aliases**, then **example phrases** (moderate strength).
- Deduplicate by entry id; prefer stronger matches.
- Return at most **3** matches (`MaxMatches`).
- Single-token matches use **word-boundary** regex to reduce noise; avoid bare words like `rule` without a catalog row.

## API

`GET /api/tickets/{id}/syniti-knowledge-context` → `SynitiKnowledgeContextMatchDto`:

- `reviewerGuidance`, `suggestedReviewerChecks[]`, `missingContextQuestions[]`
- `matchStrengthLabel`: `Strong catalog match` | `Phrase match`
- `sourceReason`: reviewer-first summary (no “heuristic”, “scraped”, or “Cortex decided” language).

## UI integration

| Location | Behavior |
|----------|----------|
| **Governance summary** | Primary SAP context when present; otherwise Syniti-led primary. Syniti section adds narrative + secondary terms; **Suggested reviewer checks** merge SAP + Syniti suggested checks (deduped, max 5). |
| **Syniti Knowledge Context card** | Reviewer guidance, optional overview, suggested checks, missing details, reference note, related concepts. |
| **Decision / routing** | **Unchanged** — disclaimers state advisory context only. |

## Backend migration path

1. v1 uses EF columns: `Aliases`, `SuggestedReviewerChecks`, `MissingContextQuestions` (`AddSynitiKnowledgeReviewerFields` migration).
2. Optional future: admin API to CRUD entries; export/import JSON; optional `RelatedSapTableHints` column.

## Manual validation tickets (demo-safe)

1. Need mapping update for YYNGM_ACTIVE on MARC from legacy active flag  
2. DSP validation rule failing for vendor master load into LFA1  
3. ADMM load error for customer master KNA1 field during mock load  
4. Source-to-target mapping missing for QMAT inspection setup  
5. Reconciliation mismatch after material master mock load  
6. Business validation needed for purchasing info record EINA/EINE mapping  

## Risks / follow-ups

- **Existing DBs** with older demo seed rows keep prior content for duplicate **terms**; new terms are still added idempotently. Operators may disable/remove obsolete sources if duplicate concepts appear.
- **Ambiguous overlaps** (e.g. both “DSP” and “Validation rule”) are limited by the top-3 cap; tune catalog overlap if the UI feels repetitive.
