# AI Security Quick Check — Cortex Guardrail

## Purpose

This is a lightweight, fast security scan used during development to catch obvious risks without running a full audit.

Run this:

- before commits
- after adding endpoints
- after modifying AI output
- after touching logging
- during rapid iteration

This is NOT a full audit.

---

## Core Rule

If something looks like internal system detail, it should NOT reach the user.

---

## Quick Checks

### 1. AI Output Safety

Ask:

- Does any AI response include:
  - GUIDs
  - SQL-like text
  - table names (dbo.*, etc.)
  - stack traces
  - internal IDs

If YES → ⚠️ flag it

---

### 2. API Response Safety

Scan endpoints for:

- returning entities instead of DTOs
- exposing:
  - OwnerId
  - RoutingRuleId
  - DecisionId
  - internal fields not needed by UI

If YES → ⚠️ flag it

---

### 3. Logging Safety

Search for:

- LogInformation
- LogError
- Console.WriteLine

Check:

- are we logging full objects?
- are we logging headers or tokens?
- are we logging AI prompts/responses?

If YES → ⚠️ flag it

---

### 4. Exception Exposure

Check:

- any `return ex.Message`
- stack traces reaching frontend

If YES → ⚠️ flag it

---

### 5. Config Safety

Check:

- any hardcoded:
  - API keys
  - connection strings
  - secrets

If YES → 🔥 critical

---

### 6. Frontend Rendering

Check UI:

- are we showing:
  - raw IDs
  - debug info
  - AI output without filtering

If YES → ⚠️ flag it

---

## Output Format

Return a simple result:

### Status
- SAFE
- WARNING
- RISK

### Findings (if any)
- short bullet list of issues

---

## Example Output

SAFE:
    No obvious security or leakage issues found.

WARNING:
    - Endpoint returns OwnerId directly
    - AI output includes GUID-like values

RISK:
    - Connection string found in config
    - Exception message returned to client

---

## Cortex Standard

- AI explains decisions, not systems
- APIs expose intent, not implementation
- Logs capture events, not secrets
- UI shows clarity, not internals

---

## Mental Model

Ask one question:

> “Would I be comfortable showing this output to a customer?”

If not → flag it