# Tier 11 Intake Learning — Aggregate Spec (Read-Only MVP)

This document specifies **what Cortex can honestly compute today** using **existing relational data** (`Tickets`, `TicketOutcomes`, `Users`, `TicketBoardDefinitions`, optional `WorkflowMetricEvents`) **without migrations, schema changes, or new product behavior.**

It supports a future **Intake Learning** report describing **follow-up friction patterns**. It **does not** claim perfect causality—only correlations visible from stored fields.

---

## 1. Purpose

- **No-migration read-only MVP**: Aggregates derive from tables that already exist. Any implementation later is additive (read/query/DTO/UI) unless product chooses structured capture (out of scope here).
- **Honest scope**: Outcomes and triage fields are **imperfect proxies** for “why” a ticket needed follow-up. The spec states those limits explicitly (see §6).
- **Goal**: Help admins see **where** return-for-detail friction clusters—**by board, priority, and requester department**—plus **how often** optional fields (`ReturnReason`, AI hint JSON) are still available for analysis.
- **Not claimed**: That a returned ticket was returned *because* of a specific missing hint, or that current triage text matches the moment of return.

---

## 2. Data Sources

| Source | What it means | Durable? | Limitations |
|--------|----------------|----------|-------------|
| **`TicketOutcome.WasReturnedForDetail`** | Once set `true` via `MarkReturnedForDetailAsync`, indicates this ticket’s lifecycle record includes at least one **Return for Detail** outcome signal. | **Yes** (boolean on outcome row) | **Not** a full event log (no history of multiple returns). **No** link to reason text or hint content on the outcome row. |
| **`Ticket.BoardId`** | Board the ticket belongs to; join to **`TicketBoardDefinitions`** for display name. | **Yes** | Board changes over time are not versioned on the ticket history in this aggregate (current board only unless you add time-travel queries). |
| **`Ticket.Priority`** | Current priority string (e.g. Critical / High / Medium / Low). | **Yes** | Priority at return time may differ from current if users changed it later—aggregates are **current priority** unless snapshotting is added later. |
| **`Ticket.CreatedBy`** | FK to **`Users.Id`** (requester / creator). | **Yes** | Used to resolve **department**; not the same as “last editor.” |
| **`User.Department`** | Optional directory field on the creator. | **Yes** while stored on user | May be **null/empty**; may **change** over time (report uses **current** department for that user unless you snapshot per ticket). |
| **`Ticket.ReturnReason`** | Reviewer-entered **free text** when **Return for Detail** is executed (API requires non-empty reason at submit). | **Conditionally** | Cleared when the ticket is approved or when flows reset return fields (e.g. requester resubmits after **NeedsMoreInfo**)—see product paths in **`TicketHandlers`**. Therefore **historical completeness** for “what was typed” erodes unless captured elsewhere at return time. |
| **`Ticket.AiTriageMissingDetailsJson`** | JSON array string of **`missingDetails`** bullets from **latest persisted AI triage** (`TicketTriagePersistence` maps to **`approvalTriagePreview.missingDetailHints`**). | **Yes** as latest snapshot | **Overwritten** when triage is regenerated—**not** guaranteed to reflect the AI state **at instant of return**. Hints are **free-text**, not enumerated categories. |
| **`WorkflowMetricEvents`** (**`reviewer_quality_signal_shown`**, **`intake_assist_completed`**, etc.) | Append-only **instrumentation**: reviewer band (`reviewerSignal`), `missingDetailHintCount`, `commentCount`; intake assist `missingDetailCount`, `clarityState`, flags. | **Yes** per event row | **`GET /api/metrics/snapshot`** exposes **global** rollups—not board/priority/department dimensions in current snapshot handler. Payloads attach **`TicketId`** when the client sends it; useful for **ad hoc** auditing, not wired as canonical per-return facts. Same honesty rules: **not authoritative** taxonomy. |

**Universe caveat for aggregates**: Prefer joining **`Tickets`** to **`TicketOutcomes`** on **`Tickets.Id == TicketOutcomes.TicketId`**. Tickets **without** an outcome row yet (no lifecycle row created) **drop out** unless the query **left**-joins and treats missing outcome as non-returned.

---

## 3. Proposed Aggregates

**Shared definitions**

- **`returnedTickets`**: Distinct ticket count where **`TicketOutcomes.WasReturnedForDetail == true`** (and optionally **time/filter** predicates on **`Tickets.CreatedDate`** or **`TicketOutcomes.LastUpdatedAtUtc`** if product defines a reporting window).

- **`totalTickets`**: Population definition must be explicit in the product—in practice one of:

  - **A.** All **`Tickets`** in scope (board/date filters); **LEFT JOIN** outcome; treat null outcome / `WasReturnedForDetail == false` as not returned **for rate denominator**, or  

  - **B.** Only **`Tickets`** with an **`TicketOutcomes`** row (strict lifecycle cohort).

The examples below assume **B** (inner join outcome) unless stated—document the chosen denominator in implementation.

---

### Return rate by board

| Field | Description |
|-------|-------------|
| **`boardId`** | `Tickets.BoardId` |
| **`boardName`** | `TicketBoardDefinitions.Name` **where** `Id == boardId` (nullable join) |
| **`totalTickets`** | Count of tickets in cohort with this `BoardId` |
| **`returnedTickets`** | Count where **`WasReturnedForDetail`** |
| **`returnRatePercent`** | `100.0 * returnedTickets / totalTickets` (guard **divide-by-zero**) |

---

### Return rate by priority

| Field | Description |
|-------|-------------|
| **`priority`** | `Tickets.Priority` (current stored value) |
| **`totalTickets`** | Cohort count for that priority |
| **`returnedTickets`** | **`WasReturnedForDetail`** count |
| **`returnRatePercent`** | As above |

---

### Return rate by requester department

| Field | Description |
|-------|-------------|
| **`department`** | `Users.Department` **trimmed**, or sentinel **`'(Unknown)'`** when null/empty |
| **`totalTickets`** | Cohort count |
| **`returnedTickets`** | **`WasReturnedForDetail`** count |
| **`returnRatePercent`** | As above |
| **`unknownDepartmentCount`** | Optional: count of tickets whose creator has no department (useful for footnotes) |

---

### Return reason availability

Among tickets with **`WasReturnedForDetail == true`** (optionally same date/window):

| Field | Description |
|-------|-------------|
| **`returnedTickets`** | Denominator (all returned in scope) |
| **`returnReasonStillAvailableCount`** | Count where **`Ticket.ReturnReason` IS NOT NULL AND LTRIM(RTRIM(ReturnReason)) <> ''** |
| **`returnReasonAvailabilityPercent`** | `100.0 * returnReasonStillAvailableCount / returnedTickets` |

Interprets “availability” as **current row still holds text**, not “we know what was said at return time for 100% of history.”

---

### Missing-detail hint presence (AI triage snapshot)

Among **`WasReturnedForDetail == true`** tickets:

| Field | Description |
|-------|-------------|
| **`returnedTickets`** | Denominator |
| **`returnedTicketsWithMissingHintJson`** | Count where **`AiTriageMissingDetailsJson`** is not null/not whitespace **and** deserializes to a **non-empty** string array |
| **`averageMissingHintCount`** | Average of **hint counts** over returned cohort (non-null JSON only—define whether zero counts as contribution) |
| **`missingHintCountBuckets`** (simple) | e.g. `0`, `1-2`, `3-4`, `"5+"`** based on array length |

---

### Optional global context (metrics table)

Not a substitute for relational aggregates:

- **`WorkflowMetricEvents`** with **`EventType = 'reviewer_quality_signal_shown'`** / **`'intake_assist_completed'`** can support **coarse** global trend lines (already partially summarized in **`WorkflowMetricsSnapshot`**). **Do not** merge naively with outcome tables without a clear join key and time alignment.

---

## 4. Example LINQ Queries

Illustrative **EF Core–style** queries (namespaces omitted). Adjust **AsNoTracking()**, filters, and **totalTickets** definition for product rules.

### Group by board (with board name)

```csharp
// Cohort: tickets that have a TicketOutcome row (inner join).
var boardStats = await (
        from t in db.Tickets.AsNoTracking()
        join o in db.TicketOutcomes.AsNoTracking() on t.Id equals o.TicketId
        join b in db.TicketBoardDefinitions.AsNoTracking() on t.BoardId equals b.Id into bJoin
        from b in bJoin.DefaultIfEmpty()
        group new { t, o, b } by new { t.BoardId, BoardName = b != null ? b.Name : null }
        into g
        select new
        {
            g.Key.BoardId,
            g.Key.BoardName,
            TotalTickets = g.Count(),
            ReturnedTickets = g.Count(x => x.o.WasReturnedForDetail),
        })
    .ToListAsync(cancellationToken);

// Post-process return rate % in memory (avoid provider quirks):
var withRate = boardStats.Select(x => new
{
    x.BoardId,
    x.BoardName,
    x.TotalTickets,
    ReturnedTickets = x.ReturnedTickets,
    ReturnRatePercent = x.TotalTickets == 0
        ? 0m
        : Math.Round(100m * x.ReturnedTickets / x.TotalTickets, 2),
}).OrderByDescending(x => x.ReturnRatePercent);
```

### Group by priority

```csharp
var priorityStats = await (
        from t in db.Tickets.AsNoTracking()
        join o in db.TicketOutcomes.AsNoTracking() on t.Id equals o.TicketId
        group new { t, o } by t.Priority into g
        select new
        {
            Priority = g.Key,
            TotalTickets = g.Count(),
            ReturnedTickets = g.Count(x => x.o.WasReturnedForDetail),
        })
    .ToListAsync(cancellationToken);
```

### Join `CreatedBy` → `Users` for department

```csharp
var deptStats = await (
        from t in db.Tickets.AsNoTracking()
        join o in db.TicketOutcomes.AsNoTracking() on t.Id equals o.TicketId
        join u in db.Users.AsNoTracking() on t.CreatedBy equals u.Id into uJoin
        from u in uJoin.DefaultIfEmpty()
        let dept = u == null || string.IsNullOrWhiteSpace(u.Department)
            ? "(Unknown)"
            : u.Department.Trim()
        group new { t, o, u } by dept into g
        select new
        {
            Department = g.Key,
            TotalTickets = g.Count(),
            ReturnedTickets = g.Count(x => x.o.WasReturnedForDetail),
            UnknownDepartmentCount = g.Count(x =>
                x.u == null || string.IsNullOrWhiteSpace(x.u.Department)),
        })
    .ToListAsync(cancellationToken);
```

### Return reason availability (returned subset)

```csharp
var returnedQuery =
    from t in db.Tickets.AsNoTracking()
    join o in db.TicketOutcomes.AsNoTracking() on t.Id equals o.TicketId
    where o.WasReturnedForDetail
    select t;

var returnedTickets = await returnedQuery.CountAsync(cancellationToken);

var stillHasReason = await returnedQuery
    .CountAsync(t =>
        t.ReturnReason != null && t.ReturnReason.Trim().Length > 0,
        cancellationToken);

var availabilityPercent = returnedTickets == 0
    ? 0m
    : Math.Round(100m * stillHasReason / returnedTickets, 2);
```

### Missing hints — deserialize and count (client-side or raw SQL)

EF cannot easily aggregate JSON array lengths in all providers; options:

- **Fetch** `Id`, `AiTriageMissingDetailsJson` for returned tickets **in scope** and compute in memory **(bounded batch)** or  

- **SQL Server** `OPENJSON` / JSON functions in raw SQL (see §5).

```csharp
var returnedIds = await (
        from t in db.Tickets.AsNoTracking()
        join o in db.TicketOutcomes.AsNoTracking() on t.Id equals o.TicketId
        where o.WasReturnedForDetail
        select new { t.Id, t.AiTriageMissingDetailsJson })
    .ToListAsync(cancellationToken);

static int CountHints(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return 0;
    try
    {
        var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
        return list?.Count(s => !string.IsNullOrWhiteSpace(s)) ?? 0;
    }
    catch { return 0; }
}

var withHints = returnedIds.Count(x => CountHints(x.AiTriageMissingDetailsJson) > 0);
var avg = returnedIds.Count == 0 ? 0d
    : returnedIds.Average(x => CountHints(x.AiTriageMissingDetailsJson));
```

---

## 5. Equivalent SQL Sketches

**Assumption**: Tables `Tickets`, `TicketOutcomes`, `Users`, `TicketBoardDefinitions`; `Tickets.Id` = `TicketOutcomes.TicketId`. SQL dialect may need tweaks (e.g. JSON).

### By board

```sql
SELECT
    t.BoardId,
    bd.[Name] AS BoardName,
    COUNT(*) AS TotalTickets,
    SUM(CASE WHEN o.WasReturnedForDetail = 1 THEN 1 ELSE 0 END) AS ReturnedTickets,
    CAST(100.0 * SUM(CASE WHEN o.WasReturnedForDetail = 1 THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(6,2)) AS ReturnRatePercent
FROM dbo.Tickets t
INNER JOIN dbo.TicketOutcomes o ON o.TicketId = t.Id
LEFT JOIN dbo.TicketBoardDefinitions bd ON bd.Id = t.BoardId
GROUP BY t.BoardId, bd.[Name]
ORDER BY ReturnRatePercent DESC;
```

### By priority

```sql
SELECT
    t.Priority,
    COUNT(*) AS TotalTickets,
    SUM(CASE WHEN o.WasReturnedForDetail = 1 THEN 1 ELSE 0 END) AS ReturnedTickets,
    CAST(100.0 * SUM(CASE WHEN o.WasReturnedForDetail = 1 THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(6,2)) AS ReturnRatePercent
FROM dbo.Tickets t
INNER JOIN dbo.TicketOutcomes o ON o.TicketId = t.Id
GROUP BY t.Priority
ORDER BY ReturnRatePercent DESC;
```

### By requester department (current `Users.Department`)

```sql
SELECT
    COALESCE(NULLIF(LTRIM(RTRIM(u.Department)), ''), '(Unknown)') AS Department,
    COUNT(*) AS TotalTickets,
    SUM(CASE WHEN o.WasReturnedForDetail = 1 THEN 1 ELSE 0 END) AS ReturnedTickets,
    CAST(100.0 * SUM(CASE WHEN o.WasReturnedForDetail = 1 THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(6,2)) AS ReturnRatePercent,
    SUM(CASE WHEN NULLIF(LTRIM(RTRIM(u.Department)), '') IS NULL THEN 1 ELSE 0 END) AS UnknownDepartmentCount
FROM dbo.Tickets t
INNER JOIN dbo.TicketOutcomes o ON o.TicketId = t.Id
LEFT JOIN dbo.Users u ON u.Id = t.CreatedBy
GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(u.Department)), ''), '(Unknown)')
ORDER BY ReturnRatePercent DESC;
```

### Return reason availability

```sql
DECLARE @Returned INT = (
    SELECT COUNT(*)
    FROM dbo.Tickets t
    INNER JOIN dbo.TicketOutcomes o ON o.TicketId = t.Id
    WHERE o.WasReturnedForDetail = 1
);

DECLARE @StillHasReason INT = (
    SELECT COUNT(*)
    FROM dbo.Tickets t
    INNER JOIN dbo.TicketOutcomes o ON o.TicketId = t.Id
    WHERE o.WasReturnedForDetail = 1
      AND t.ReturnReason IS NOT NULL AND LTRIM(RTRIM(t.ReturnReason)) <> ''
);

SELECT
    @Returned AS ReturnedTickets,
    @StillHasReason AS ReturnReasonStillAvailableCount,
    CAST(CASE WHEN @Returned = 0 THEN 0 ELSE 100.0 * @StillHasReason / @Returned END AS DECIMAL(6,2)) AS ReturnReasonAvailabilityPercent;
```

### Hint count — SQL Server JSON (sketch)

```sql
SELECT
    COUNT(*) AS ReturnedTickets,
    SUM(CASE WHEN t.AiTriageMissingDetailsJson IS NOT NULL AND t.AiTriageMissingDetailsJson <> '[]' THEN 1 ELSE 0 END) AS WithNonEmptyJson,
    AVG(CAST((SELECT COUNT(*) FROM OPENJSON(t.AiTriageMissingDetailsJson)) AS FLOAT)) AS AvgHintCount
FROM dbo.Tickets t
INNER JOIN dbo.TicketOutcomes o ON o.TicketId = t.Id
WHERE o.WasReturnedForDetail = 1;
```

*Requires valid JSON array; malformed rows need app-side handling. Validate against sample data.*

---

## 6. Limitations / Honesty Notes

1. **`ReturnReason` may be cleared** after requester resubmission or when approval flows clear return fields—**availability %** measures **current DB state**, not complete historical capture of reviewer text.
2. **`AiTriageMissingDetailsJson`** is the **latest** triage snapshot, **not** guaranteed to be the snapshot **at return time**—use as **“current hint pressure among returned tickets”**, not forensic proof.
3. **Missing hints are free-text**—you can count bullets and compare cohorts, but **not** emit stable global categories (“environment”, “vendor number”) without taxonomy or NLP (out of scope for this MVP).
4. **`User.Department`** may be **missing** or **changed**—aggregates use **current** profile value.
5. **`WasReturnedForDetail`** is **durable** but **boolean**—no per-return history, timestamps of return are **not** on `TicketOutcome` (return timing lives on **`Ticket.ReturnedForDetailAt`** while not cleared).
6. **Correlation, not causation**: High return rate on a board may reflect **intake quality**, **board policy**, **reviewer behavior**, or **selection bias**—frame as **follow-up friction patterns**.

---

## 7. Recommended Future Structured Capture (Not Implemented)

If product needs **auditable, time-aligned** intake learning:

| Concept | Sketch |
|--------|--------|
| **`TicketReturnDetailEvent`** (or **`TicketIntakeSignal`**) | One row **per return event** (or per snapshot capture). |
| **`OccurredUtc`** | When reviewer returned (or system recorded). |
| **`TicketId`**, **`BoardId`**, **`Priority`**, **`RequesterDepartmentSnapshot`** | Frozen dimensions at event time (**denormalized** intentional). |
| **`ReturnReason`**, **`MissingHintSnapshotJson`** | Copies at event time so later edits do not erase history. |
| **Optional **`Category`/`Tags`**** | Controlled vocabulary for reporting. |
| **`ReviewerUserId`** | From **`ReturnedForDetailBy`** at event time if desired. |

**No migration or implementation here**—this is a roadmap placeholder.

---

## 8. MVP Report Copy (Buyer-Friendly)

Use **short disclaimers** in footnotes or captions.

| Section headline | Supporting line |
|------------------|-----------------|
| **Returned tickets by board** | “Share of tracked tickets whose outcome indicates they were returned for more detail.” |
| **Follow-up friction by priority** | “Return-for-detail signals by current ticket priority.” |
| **Follow-up friction by requester department** | “Based on creator’s department when available; ‘Unknown’ includes missing directory data.” |
| **Return reason availability** | “How often the free-text return reason is still stored on the ticket—affected when requesters revise after return.” |
| **Missing-detail hint pressure (AI)** | “How often returned tickets currently show AI-listed missing detail bullets (**latest triage**, not necessarily from the moment of return).” |
| **Global disclaimer** | “These insights are based on ticket outcomes and current ticket/triage snapshots. They indicate patterns and friction, not root cause.” |

---

## 9. Recommended Next Implementation Task

**Task title**: Implement **read-only** reporting API returning **JSON DTOs** for §3 aggregates (**board**, **priority**, **department**, **return reason availability**, **missing-hint summaries**).

**Scope**

- Add a **minimal** handler/service (e.g. `GET /api/reports/intake-learning/aggregates` or under existing reports/metrics namespace per project conventions)—**authenticate/authorize admin or report role** per product rules.
- **No migration**—EF Core queries joining **`Tickets`**, **`TicketOutcomes`**, **`Users`**, **`TicketBoardDefinitions`** only.
- **Unit/integration tests**: snapshot or golden-file on seeded data; edge cases (`totalTickets == 0`, unknown department).
- **No UI** in same PR (or optional stub only if explicitly requested)—**Reports page** consumes in a **follow-up** task.

**Verification**: `dotnet test`; manual hit with bearer token comparing counts to SQL sketches on staging.

---

*Document version: Tier 11 Task 5A — aligns with **`Ticket`**, **`TicketOutcome`**, **`User`**, **`TicketBoardDefinition`** models and workflow metric patterns in codebase as of authoring.*
