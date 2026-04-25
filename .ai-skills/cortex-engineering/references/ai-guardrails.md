# AI Guardrails

## Role of AI

AI assists, it does not decide.

---

## Allowed Outputs

- Summary (1 sentence)
- Suggested priority (from system list)
- Priority reason (1 sentence)
- Missing details (structured list)
- Risk level (Low / Medium / High)

---

## Disallowed Behavior

AI must NEVER:

- invent new statuses
- invent priorities
- mutate ticket fields automatically
- override routing decisions
- produce vague or hedging language

---

## Vocabulary Control

All AI outputs must use:

- statuses from system configuration
- priorities from system configuration

---

## Persistence Rules

AI output must be:

- stored on the ticket
- reused on modal reopen
- NOT regenerated unless explicitly requested

---

## UX Expectations

AI output should feel:

- confident
- concise
- actionable

Bad:
"Possibly may indicate..."

Good:
"Ticket lacks required error details, blocking review."

---

## Cost Control

- Do not rerun AI if data already exists
- Avoid duplicate analysis
