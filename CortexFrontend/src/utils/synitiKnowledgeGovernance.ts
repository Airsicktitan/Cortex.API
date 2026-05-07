import type { SynitiKnowledgeContextMatch } from "../types/synitiKnowledgeContext";

/** Merge SAP- and Syniti-derived checks; dedupe case-insensitively; cap total for readability. */
export function mergeGovernanceReviewerChecks(args: {
  sapChecks: string[];
  synitiMatches: SynitiKnowledgeContextMatch[];
  extraBullets: string[];
  maxTotal: number;
}): string[] {
  const { sapChecks, synitiMatches, extraBullets, maxTotal } = args;
  const synitiFlat: string[] = [];
  for (const m of synitiMatches) {
    for (const line of m.suggestedReviewerChecks ?? []) {
      const t = line.trim();
      if (t) {
        synitiFlat.push(t);
      }
    }
  }

  return dedupeStrings([...sapChecks, ...synitiFlat, ...extraBullets], maxTotal);
}

function dedupeStrings(items: string[], max: number): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const item of items) {
    const t = item.trim();
    const k = t.toLowerCase();
    if (!t || seen.has(k)) {
      continue;
    }
    seen.add(k);
    out.push(t);
    if (out.length >= max) {
      break;
    }
  }
  return out;
}

export function buildSynitiPrimarySummaryLine(
  m: SynitiKnowledgeContextMatch | undefined,
): string | null {
  if (!m) {
    return null;
  }

  const term = m.term.trim();
  const guide = m.reviewerGuidance?.trim() || m.shortDefinition?.trim();
  if (!guide) {
    return null;
  }

  return `This appears related to ${term}. ${guide}`;
}
