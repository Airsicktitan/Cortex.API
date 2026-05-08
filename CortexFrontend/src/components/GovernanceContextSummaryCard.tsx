import type { SapTicketReferenceMatch } from "../types/sapTicketReference";
import type { SynitiKnowledgeContextMatch } from "../types/synitiKnowledgeContext";
import { formatDisplayValue } from "../utils/presentation";
import { isCustomFieldSignal } from "../utils/sapReferenceMetadataSignals";
import {
  getSapGovernanceCardPrimaryContext,
  getSapGovernanceCardReviewerChecks,
} from "../utils/sapReviewerGuidance";
import { mergeGovernanceReviewerChecks, buildSynitiPrimarySummaryLine } from "../utils/synitiKnowledgeGovernance";
import { GOVERNANCE_ADVISORY_BOUNDARY } from "../utils/governanceAdvisoryCopy";

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
    return "Likely custom SAP field (extension / customer-specific)";
  }

  const bo = formatDisplayValue(m.businessObject);
  if (bo !== "—") {
    return truncateReviewLine(`${bo} context`, 72);
  }

  const mod = formatDisplayValue(m.module);
  if (mod !== "—") {
    return truncateReviewLine(`${mod} in SAP`, 72);
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

function buildSapDataSignals(
  matches: SapTicketReferenceMatch[],
  maxRows: number,
): DataSignalRow[] {
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
    if (out.length >= maxRows) {
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

  if (out.length < maxRows) {
    for (const m of matches) {
      if (out.length >= maxRows) {
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

  if (out.length < maxRows) {
    for (const m of matches) {
      if (out.length >= maxRows) {
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

function buildGovernanceSynitiBlock(
  matches: SynitiKnowledgeContextMatch[],
  sapPrimaryShown: boolean,
): {
  sectionPrimaryLine: string | null;
  secondaryLines: { headline: string; trail: string }[];
} {
  if (matches.length === 0) {
    return { sectionPrimaryLine: null, secondaryLines: [] };
  }

  const sectionPrimaryLine = sapPrimaryShown
    ? buildSynitiPrimarySummaryLine(matches[0])
    : null;

  const secondaryLines = matches.slice(1, 6).map((m) => ({
    headline: m.term.trim(),
    trail: truncateReviewLine(m.shortDefinition.trim(), 100),
  }));

  return { sectionPrimaryLine, secondaryLines };
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
  const hasSapRef = sapMatches.length > 0;

  if (hasSapField) {
    bullets.push(
      "Confirm whether the referenced field is standard SAP or a customer extension.",
    );
  }

  if (hasSapDomain || domPreview) {
    bullets.push("Check whether domain values or lookup values are required.");
  }

  if (hasSapRef && synitiMatches.length === 0) {
    bullets.push(
      "Consider mapping, validation, migration paths, and governance ownership if the request affects those areas.",
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

  const primarySapContext =
    sapMatches.length > 0 ? getSapGovernanceCardPrimaryContext(sapMatches) : null;
  const synitiOnlyPrimary =
    !primarySapContext && synitiMatches.length > 0
      ? buildSynitiPrimarySummaryLine(synitiMatches[0])
      : null;
  const sapReviewerChecks =
    sapMatches.length > 0 ? getSapGovernanceCardReviewerChecks(sapMatches) : [];
  const sapDataSignalBudget = synitiMatches.length > 0 ? 3 : 2;
  const dataSignals = buildSapDataSignals(sapMatches, sapDataSignalBudget);
  const { sectionPrimaryLine: synitiSectionPrimary, secondaryLines: synitiSecondary } =
    buildGovernanceSynitiBlock(synitiMatches, Boolean(primarySapContext));
  const extraFocus = buildReviewerFocusBullets({ sapMatches, synitiMatches });
  const reviewerFocus = mergeGovernanceReviewerChecks({
    sapChecks: sapReviewerChecks,
    synitiMatches,
    extraBullets: extraFocus,
    maxTotal: 5,
  });

  return (
    <section className="rounded-md border border-slate-400/55 bg-white/90 px-3 py-2.5 shadow-sm ring-1 ring-slate-900/[0.05] dark:border-slate-500/60 dark:bg-slate-900/55 dark:ring-white/[0.06]">
      <header className="space-y-1">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-700 dark:text-slate-300">
          Governance summary
        </h3>
        <p className="text-[11px] leading-snug text-gray-600 dark:text-slate-400">
          {GOVERNANCE_ADVISORY_BOUNDARY}
        </p>
      </header>

      <div className="mt-3 space-y-3">
        {primarySapContext || synitiOnlyPrimary ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Primary context
            </p>
            <p className="mt-1 text-[11px] leading-relaxed text-gray-800 dark:text-slate-200">
              {primarySapContext ?? synitiOnlyPrimary}
            </p>
          </div>
        ) : null}

        {dataSignals.length > 0 ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Referenced objects
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

        {synitiSectionPrimary || synitiSecondary.length > 0 ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Syniti knowledge
            </p>
            {synitiSectionPrimary ? (
              <p className="mt-1 text-[11px] leading-relaxed text-gray-800 dark:text-slate-200">
                {synitiSectionPrimary}
              </p>
            ) : null}
            {synitiSecondary.length > 0 ? (
              <ul className="mt-1.5 list-outside list-disc space-y-1 pl-4 text-[11px] leading-relaxed text-gray-800 dark:text-slate-200">
                {synitiSecondary.map((row) => (
                  <li key={`${row.headline}:${row.trail}`}>
                    <span className="font-semibold text-gray-900 dark:text-slate-100">
                      {row.headline}
                    </span>
                    {" — "}
                    <span className="text-gray-700 dark:text-slate-300">{row.trail}</span>
                  </li>
                ))}
              </ul>
            ) : null}
          </div>
        ) : null}

        {reviewerFocus.length > 0 ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Suggested reviewer checks
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
        Does not assign owners, change routing, or replace your approval judgment.
      </p>
    </section>
  );
}
