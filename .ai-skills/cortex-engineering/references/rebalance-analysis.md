# Rebalance Analysis Skill

## Purpose

Use this skill only after Cortex has produced deterministic rebalance candidates.

AI explains the decision. It does not choose the owner.

## Authority Order

1. Routing rules decide eligibility.
2. Workload scoring ranks viable owners.
3. Distribution logic prevents pileups in the current rebalance run.
4. AI explains the selected deterministic recommendation.
5. Safety validation blocks stale or conflicted execution.

Hard rules always win over AI language.

## Prompt Rules

The model must:

- explain why the selected ticket is worth moving
- explain why the selected owner won
- compare alternatives without inventing candidates
- call out diversification when the final owner differs from the raw top scorer
- keep language concise, executive, and product-facing
- use only owner names and user ids present in the decision packet

The model must not:

- assign or change the final owner
- suggest users outside the candidate list
- mutate priority, status, board, SLA, or routing fields
- introduce hidden business rules
- make unsupported claims about capacity, dates, or commitments
- hedge with "might", "maybe", "probably", or "appears"

## Output Shape

Return advisory language only:

- rationale
- risk summary
- tradeoff summary
- confidence wording

If the model cannot produce constrained language, Cortex should use deterministic explanation fields without AI text.
