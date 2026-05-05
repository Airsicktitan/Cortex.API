import type { SapTicketReferenceMatch } from "../types/sapTicketReference";
import type { SynitiKnowledgeContextMatch } from "../types/synitiKnowledgeContext";
import { formatDisplayValue } from "../utils/presentation";
import { isCustomFieldSignal } from "../utils/sapReferenceMetadataSignals";

function truncateReviewLine(text: string, maxLen: number): string {
  const t = text.trim();
  if (t.length <= maxLen) {
    return t;
  }

  return `${t.slice(0, Math.max(1, maxLen - 1))}…`;
}

function normTableKey(name: string): string {
  return name.trim().toUpperCase();
}

function sapFieldTrail(m: SapTicketReferenceMatch): string {
  const ext =
    Boolean(m.likelyCustomerExtensionField) || isCustomFieldSignal(m);
  if (ext) {
    return "Likely extension or customer-specific field";
  }

  const bo = formatDisplayValue(m.businessObject);
  if (bo !== "—") {
    return truncateReviewLine(`${bo} context`, 72);
  }

  const mod = formatDisplayValue(m.module);
  if (mod !== "—") {
    return truncateReviewLine(`${mod} metadata context`, 72);
  }

  const td = m.tableDescription?.trim();
  if (td) {
    return truncateReviewLine(td, 88);
  }

  return "SAP reference catalog context";
}

function sapTableTrail(m: SapTicketReferenceMatch): string {
  const td = m.tableDescription?.trim();
  if (td) {
    return truncateReviewLine(td, 92);
  }

  const parts = [
    formatDisplayValue(m.module),
    formatDisplayValue(m.businessObject),
  ].filter((p) => p !== "—");

  return parts.length > 0 ? `${parts.join(" · ")} context` : "SAP reference catalog context";
}

function sapDomainTrail(m: SapTicketReferenceMatch): string {
  if (m.domainValuesPreview?.trim()) {
    return truncateReviewLine(m.domainValuesPreview.trim(), 88);
  }

  return truncateReviewLine("Controlled domain values may apply.", 72);
}

type DataSignalRow = {
  headline: string;
  trail: string;
};

function buildSapDataSignals(matches: SapTicketReferenceMatch[]): DataSignalRow[] {
  const out: DataSignalRow[] = [];
  const seen = new Set<string>();

  const tryPush = (key: string, row: DataSignalRow) => {
    if (seen.has(key)) {
      return;
    }

    seen.add(key);
    out.push(row);
  };

  for (const m of matches) {
    if (out.length >= 2) {
      break;
    }

    if (m.matchType === "Field") {
      const table = formatDisplayValue(m.tableName);
      const field = formatDisplayValue(m.fieldName);
      if (table === "—" && field === "—") {
        continue;
      }

      tryPush(`f:${normTableKey(table)}:${field}`, {
        headline: `${table === "—" ? "SAP" : table} / ${field === "—" ? "Field" : field}`,
        trail: sapFieldTrail(m),
      });
    }
  }

  if (out.length < 2) {
    for (const m of matches) {
      if (out.length >= 2) {
        break;
      }

      if (m.matchType === "DomainValue") {
        const dn = formatDisplayValue(m.domainName);
        const dv = formatDisplayValue(m.domainValue);
        if (dn === "—" && dv === "—") {
          continue;
        }

        tryPush(`d:${normTableKey(dn)}:${normTableKey(dv)}`, {
          headline: `${dn === "—" ? "Domain" : dn} / ${dv === "—" ? "Value" : dv}`,
          trail: sapDomainTrail(m),
        });
      }
    }
  }

  if (out.length < 2) {
    for (const m of matches) {
      if (out.length >= 2) {
        break;
      }

      if (m.matchType === "Table") {
        const tn = formatDisplayValue(m.tableName);
        if (tn === "—") {
          continue;
        }

        tryPush(`t:${normTableKey(tn)}`, {
          headline: tn,
          trail: sapTableTrail(m),
        });
      }
    }
  }

  return out;
}

function buildGovernanceSignals(
  matches: SynitiKnowledgeContextMatch[],
): DataSignalRow[] {
  return matches.slice(0, 2).map((m) => ({
    headline: m.term.trim(),
    trail: truncateReviewLine(m.shortDefinition.trim(), 110),
  }));
}

function buildReviewerFocusBullets(args: {
  sapMatches: SapTicketReferenceMatch[];
  synitiMatches: SynitiKnowledgeContextMatch[];
}): string[] {
  const { sapMatches, synitiMatches } = args;
  const bullets: string[] = [];

  const hasSapField = sapMatches.some((m) => m.matchType === "Field");
  const hasSapDomain = sapMatches.some((m) => m.matchType === "DomainValue");
  const domPreview = sapMatches.some((m) => Boolean(m.domainValuesPreview?.trim()));
  const hasSyniti = synitiMatches.length > 0;
  const hasSapRef = sapMatches.length > 0;

  if (hasSapField) {
    bullets.push(
      "Confirm whether the referenced SAP field is standard or customer-specific.",
    );
  }

  if (hasSapDomain || domPreview) {
    bullets.push("Check whether domain values or lookup values are required.");
  }

  if (hasSyniti || hasSapRef) {
    bullets.push(
      "Confirm whether this request affects mapping, validation, migration execution, or governance ownership.",
    );
  }

  bullets.push("Review screenshot evidence in this tab when screenshots were analyzed.");

  return bullets;
}

/**
 * Evidence-tab summary bridging SAP catalog signals and Syniti glossary context.
 * Hidden when neither source returned matches.
 */
export function GovernanceContextSummaryCard({
  sapMatches,
  synitiMatches,
}: {
  sapMatches: SapTicketReferenceMatch[];
  synitiMatches: SynitiKnowledgeContextMatch[];
}) {
  if (sapMatches.length === 0 && synitiMatches.length === 0) {
    return null;
  }

  const dataSignals = buildSapDataSignals(sapMatches);
  const governanceSignals = buildGovernanceSignals(synitiMatches);
  const reviewerFocus = buildReviewerFocusBullets({ sapMatches, synitiMatches });

  return (
    <section className="rounded-md border border-slate-400/55 bg-white/90 px-3 py-2.5 shadow-sm ring-1 ring-slate-900/[0.05] dark:border-slate-500/60 dark:bg-slate-900/55 dark:ring-white/[0.06]">
      <header className="space-y-1">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-700 dark:text-slate-300">
          Governance Context Summary
        </h3>
        <p className="text-[11px] leading-snug text-gray-600 dark:text-slate-400">
          Cortex found reference context that may help the reviewer understand the data,
          migration, or governance impact of this ticket.
        </p>
      </header>

      <div className="mt-3 space-y-3">
        {dataSignals.length > 0 ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Data signals
            </p>
            <ul className="mt-1.5 list-outside list-disc space-y-1 pl-4 text-[11px] leading-relaxed text-gray-800 dark:text-slate-200">
              {dataSignals.map((row) => (
                <li key={`${row.headline}:${row.trail}`}>
                  <span className="font-semibold text-gray-900 dark:text-slate-100">
                    {row.headline}
                  </span>
                  {" — "}
                  <span className="text-gray-700 dark:text-slate-300">{row.trail}</span>
                </li>
              ))}
            </ul>
          </div>
        ) : null}

        {governanceSignals.length > 0 ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Governance signals
            </p>
            <ul className="mt-1.5 list-outside list-disc space-y-1 pl-4 text-[11px] leading-relaxed text-gray-800 dark:text-slate-200">
              {governanceSignals.map((row) => (
                <li key={`${row.headline}:${row.trail}`}>
                  <span className="font-semibold text-gray-900 dark:text-slate-100">
                    {row.headline}
                  </span>
                  {" — "}
                  <span className="text-gray-700 dark:text-slate-300">{row.trail}</span>
                </li>
              ))}
            </ul>
          </div>
        ) : null}

        {reviewerFocus.length > 0 ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Reviewer focus
            </p>
            <ul className="mt-1.5 list-outside list-disc space-y-1 pl-4 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
              {reviewerFocus.map((line) => (
                <li key={line}>{line}</li>
              ))}
            </ul>
          </div>
        ) : null}
      </div>

      <p className="mt-3 border-t border-gray-200/90 pt-2 text-[10px] leading-snug text-gray-500 dark:border-slate-700 dark:text-slate-500">
        Reference context only — advisory. Cortex does not connect to live SAP or Syniti
        runtime environments.
      </p>
    </section>
  );
}
