import { useMemo, useState, type ReactNode } from "react";
import type {
  SapTicketReferenceContext,
  SapTicketReferenceMatch,
} from "../types/sapTicketReference";
import { formatDisplayValue } from "../utils/presentation";
import { isCustomFieldSignal } from "../utils/sapReferenceMetadataSignals";
import {
  buildSapIntentOnlyReviewerGuidance,
  buildSapReviewerGuidance,
  type SapReviewerGuidance,
} from "../utils/sapReviewerGuidance";
import { GOVERNANCE_ADVISORY_BOUNDARY } from "../utils/governanceAdvisoryCopy";

const HELPER_COPY =
  "SAP reference context from the stored Cortex catalog (advisory). Supports review and does not change routing, owners, or approvals.";
const EVIDENCE_HELPER =
  "SAP reference matches from the stored catalog (advisory). Does not change routing, owners, or approvals.";
const SAP_REFERENCE_FOOTER = GOVERNANCE_ADVISORY_BOUNDARY;

const SAP_INTAKE_HELPER =
  "SAP-related wording is present, but no table or field matched the SAP reference catalog for this ticket.";

const SAP_REFERENCE_EMPTY_COPY =
  "No SAP reference catalog matches were found for this ticket yet.";

const INITIAL_SHOW = 5;

/** Default visible rows in embedded SAP tab; full lists + ownership require “Show more guidance”. */
const GUIDANCE_PREVIEW = {
  summary: 2,
  questions: 2,
  paths: 2,
} as const;

function pillClass(kind: "table" | "field" | "custom" | "confidence") {
  switch (kind) {
    case "table":
      return "border-slate-300/90 bg-slate-100/90 text-slate-800 dark:border-slate-600 dark:bg-slate-800/80 dark:text-slate-100";
    case "field":
      return "border-emerald-400/45 bg-emerald-100/65 text-emerald-950 dark:border-emerald-700/50 dark:bg-emerald-950/40 dark:text-emerald-100";
    case "custom":
      return "border-amber-400/55 bg-amber-100/70 text-amber-950 dark:border-amber-800/55 dark:bg-amber-950/45 dark:text-amber-100";
    case "confidence":
      return "border-slate-200/95 bg-white text-slate-700 shadow-sm dark:border-slate-600 dark:bg-slate-900/60 dark:text-slate-200";
    default:
      return "";
  }
}

const Pill = ({
  children,
  kind,
}: {
  children: ReactNode;
  kind: "table" | "field" | "custom" | "confidence";
}) => (
  <span
    className={`inline-flex max-w-full rounded-full border px-2 py-0.5 text-[11px] font-semibold leading-snug ${pillClass(kind)}`}
  >
    {children}
  </span>
);

function MatchBlock({
  m,
  layout,
}: {
  m: SapTicketReferenceMatch;
  layout: "default" | "evidence";
}) {
  const isTable = m.matchType === "Table";
  const title = isTable
    ? formatDisplayValue(m.tableName)
    : [m.tableName, m.fieldName].filter(Boolean).join(" / ") ||
      formatDisplayValue(m.fieldName);

  const secondaryLine =
    layout === "default"
      ? isTable
        ? m.tableDescription?.trim() || null
        : m.tableName
          ? `Field on ${m.tableName}`
          : "Field"
      : null;

  const fieldDetailLine =
    layout === "default" && !isTable && m.fieldDescription?.trim()
      ? m.fieldDescription
      : layout === "default" &&
          !isTable &&
          !m.fieldDescription &&
          m.tableDescription?.trim()
        ? m.tableDescription
        : null;

  const meaningLine =
    layout === "evidence"
      ? isTable
        ? m.tableDescription?.trim() || m.businessObject?.trim() || null
        : m.fieldDescription?.trim() ||
          m.tableDescription?.trim() ||
          m.businessObject?.trim() ||
          null
      : fieldDetailLine;

  const moduleLineParts = [
    m.module ? `Module: ${m.module}` : null,
    m.businessObject ? `Business object: ${m.businessObject}` : null,
  ].filter(Boolean);

  const catalogMatchLabel =
    (m.matchStrengthLabel && m.matchStrengthLabel.trim()) ||
    (m.confidence === "High"
      ? "Strong catalog match"
      : m.confidence === "Medium"
        ? "Moderate catalog match"
        : "Weaker catalog match");

  const extensionHint =
    Boolean(m.likelyCustomerExtensionField) || isCustomFieldSignal(m);

  const whyText =
    (m.sourceReason?.trim() && m.sourceReason.trim()) || m.reason?.trim();

  return (
    <div className="rounded-lg border border-slate-400/55 bg-white px-3 py-2.5 shadow-md ring-1 ring-slate-900/[0.06] dark:border-slate-500/70 dark:bg-slate-900/60 dark:ring-white/[0.08]">
      <p className="text-sm font-semibold leading-snug text-gray-900 dark:text-slate-100">
        {title}
      </p>

      <p className="mt-0.5 text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
        Table / field
      </p>

      {secondaryLine ? (
        <p className="mt-1 text-[11px] leading-snug text-gray-600 dark:text-slate-400">
          {secondaryLine}
        </p>
      ) : null}

      <div className="mt-2 flex flex-wrap gap-1.5">
        {isTable ? (
          <Pill kind="table">Table</Pill>
        ) : (
          <>
            <Pill kind="field">Field</Pill>
            {extensionHint ? (
              <Pill kind="custom">Likely custom SAP field</Pill>
            ) : null}
          </>
        )}
        {layout !== "evidence" ? (
          <Pill kind="confidence">{catalogMatchLabel}</Pill>
        ) : null}
      </div>

      {meaningLine && layout === "evidence" ? (
        <>
          <p className="mt-2 text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Catalog description
          </p>
          <p className="mt-0.5 text-[11px] leading-snug text-gray-700 dark:text-slate-300">
            {meaningLine}
          </p>
        </>
      ) : null}

      {layout === "default" &&
      fieldDetailLine &&
      fieldDetailLine !== secondaryLine ? (
        <p className="mt-2 text-[11px] leading-snug text-gray-600 dark:text-slate-400">
          {fieldDetailLine}
        </p>
      ) : null}

      {layout === "default" && isTable && moduleLineParts.length > 0 ? (
        <p className="mt-2 text-[11px] leading-snug text-gray-600 dark:text-slate-400">
          <span className="font-medium text-gray-500 dark:text-slate-500">
            Context:{" "}
          </span>
          {moduleLineParts.join(" · ")}
        </p>
      ) : null}

      {whyText ? (
        <>
          <p className="mt-2 text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Match detail
          </p>
          <p className="mt-0.5 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
            {whyText}
          </p>
        </>
      ) : null}

      {m.domainValuesPreview?.trim() ? (
        <>
          <p className="mt-2 text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Domain values (catalog preview)
          </p>
          <p className="mt-0.5 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
            {m.domainValuesPreview.trim()}
          </p>
        </>
      ) : null}

      {layout === "default" ? (
        <p className="mt-2 text-[11px] leading-snug text-gray-500 dark:text-slate-500">
          Imported catalog source: {formatDisplayValue(m.sourceName)}
        </p>
      ) : (
        <p className="mt-2 text-[10px] text-gray-500 dark:text-slate-500">
          Source: {formatDisplayValue(m.sourceName)}
        </p>
      )}
    </div>
  );
}

function ReviewerGuidanceBlock({ guidance }: { guidance: SapReviewerGuidance }) {
  const [expanded, setExpanded] = useState(false);

  const list = (items: string[]) => (
    <ul className="mt-1 list-outside list-disc space-y-1 pl-3.5 text-xs leading-5 text-gray-700 dark:text-slate-300">
      {items.map((line, i) => (
        <li key={`${i}-${line.slice(0, 48)}`}>{line}</li>
      ))}
    </ul>
  );

  const sAll = guidance.summaryLines;
  const qAll = guidance.questions;
  const pAll = guidance.investigationPaths;
  const oAll = guidance.ownershipHints;

  const sVis = expanded ? sAll : sAll.slice(0, GUIDANCE_PREVIEW.summary);
  const qVis = expanded ? qAll : qAll.slice(0, GUIDANCE_PREVIEW.questions);
  const pVis = expanded ? pAll : pAll.slice(0, GUIDANCE_PREVIEW.paths);
  const oVis = expanded ? oAll : [];

  const hasMoreCollapsed =
    sAll.length > GUIDANCE_PREVIEW.summary ||
    qAll.length > GUIDANCE_PREVIEW.questions ||
    pAll.length > GUIDANCE_PREVIEW.paths ||
    oAll.length > 0;

  return (
    <section
      className="rounded-md border border-dashed border-gray-300/90 bg-gray-50/40 px-3 py-3 dark:border-slate-600/70 dark:bg-slate-900/25"
      aria-label="SAP reviewer guidance"
    >
      <h4 className="text-[11px] font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
        Reviewer guidance
      </h4>
      <p className="mt-1 text-[11px] leading-4 text-gray-600 dark:text-slate-500">
        Advisory context only. Does not assign owners, change routing, or approvals.
      </p>

      {sVis.length > 0 ? (
        <div className="mt-3">
          <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
            SAP context
          </p>
          {list(sVis)}
        </div>
      ) : null}

      {qVis.length > 0 ? (
        <div className="mt-3">
          <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
            Reviewer checks
          </p>
          {list(qVis)}
        </div>
      ) : null}

      {pVis.length > 0 ? (
        <div className="mt-3">
          <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
            Suggested follow-up
          </p>
          {list(pVis)}
        </div>
      ) : null}

      {oVis.length > 0 ? (
        <div className="mt-3 border-t border-gray-200/80 pt-3 dark:border-slate-600/60">
          <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
            Potential ownership areas
          </p>
          {list(oVis)}
        </div>
      ) : null}

      {hasMoreCollapsed ? (
        <button
          type="button"
          onClick={() => setExpanded((e) => !e)}
          className="mt-3 text-[11px] font-semibold leading-4 text-cortex-blue hover:underline dark:text-emerald-300"
        >
          {expanded ? "Show less guidance" : "Show more guidance"}
        </button>
      ) : null}
    </section>
  );
}

export type SapTicketReferenceContextCardVariant = "standalone" | "embedded";

export function SapTicketReferenceContextCard({
  context,
  loading,
  loadError,
  variant = "standalone",
  purpose = "default",
  ticketTitle,
  ticketDescription,
}: {
  context: SapTicketReferenceContext | null;
  loading: boolean;
  loadError: boolean;
  /** `embedded`: no outer card border; for Cortex tab panels. */
  variant?: SapTicketReferenceContextCardVariant;
  /** `evidence`: concise Evidence-tab reviewer layout. */
  purpose?: "default" | "evidence";
  /** Optional ticket text for key/required phrasing in catalog-matched guidance. */
  ticketTitle?: string | null;
  ticketDescription?: string | null;
}) {
  const isEvidencePurpose = purpose === "evidence";
  const matchLayout = isEvidencePurpose ? "evidence" : "default";
  const [showAll, setShowAll] = useState(false);

  const matches = context?.matches ?? [];
  const isSapIntentOnly =
    Boolean(context?.sapIntentOnly) && matches.length === 0;
  const reviewerGuidance = useMemo((): SapReviewerGuidance | null => {
    if (!context) {
      return null;
    }
    if (isSapIntentOnly) {
      const ticketBody = [ticketTitle, ticketDescription]
        .filter((s) => Boolean(s?.trim()))
        .join("\n");
      return buildSapIntentOnlyReviewerGuidance(
        ticketBody.length > 0 ? ticketBody : undefined,
      );
    }
    if (!matches.length) {
      return null;
    }
    const ticketBody = [ticketTitle, ticketDescription]
      .filter((s) => Boolean(s?.trim()))
      .join("\n");
    return buildSapReviewerGuidance(
      matches,
      ticketBody.length > 0 ? ticketBody : undefined,
    );
  }, [context, isSapIntentOnly, matches, ticketTitle, ticketDescription]);
  const visible = useMemo(() => {
    if (showAll || matches.length <= INITIAL_SHOW) {
      return matches;
    }
    return matches.slice(0, INITIAL_SHOW);
  }, [matches, showAll]);

  if (loading) {
    const body = (
      <p className="text-[11px] text-gray-500 dark:text-slate-500">
        Loading SAP reference context…
      </p>
    );
    if (isEvidencePurpose) {
      return (
        <section className="rounded-md border border-gray-200/90 bg-gray-50/70 px-3 py-2.5 dark:border-slate-700 dark:bg-slate-900/40">
          <header className="space-y-1">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
              SAP Reference Context
            </h3>
          </header>
          <div className="mt-2">{body}</div>
        </section>
      );
    }
    if (variant === "embedded") {
      return body;
    }
    return null;
  }

  const errorBlock = (
    <p className="text-[11px] text-gray-600 dark:text-slate-400">
      Unable to load SAP reference context.
    </p>
  );

  if (loadError) {
    if (isEvidencePurpose) {
      return (
        <section className="rounded-md border border-amber-200/90 bg-amber-50/60 px-3 py-2.5 dark:border-amber-800/55 dark:bg-amber-950/25">
          <header className="space-y-1">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
              SAP Reference Context
            </h3>
          </header>
          <div className="mt-2">{errorBlock}</div>
        </section>
      );
    }
    return variant === "standalone" ? (
      <section className="rounded-md border border-gray-200/90 bg-gray-50/70 px-3 py-2 dark:border-slate-700 dark:bg-slate-900/40">
        {errorBlock}
      </section>
    ) : (
      <div className="text-[11px] text-gray-500 dark:text-slate-500">
        {errorBlock}
      </div>
    );
  }

  if (
    purpose !== "evidence" &&
    (!context || (matches.length === 0 && !isSapIntentOnly))
  ) {
    return null;
  }

  if (
    purpose === "evidence" &&
    context &&
    matches.length === 0 &&
    !isSapIntentOnly
  ) {
    return (
      <section className="rounded-md border border-gray-200/90 bg-gray-50/70 px-3 py-2.5 dark:border-slate-700 dark:bg-slate-900/40">
        <header className="space-y-1">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
            SAP Reference Context
          </h3>
          <p className="text-[11px] leading-snug text-gray-600 dark:text-slate-400">
            {SAP_REFERENCE_EMPTY_COPY}
          </p>
        </header>
        <p className="mt-2 border-t border-gray-200/90 pt-2 text-[10px] leading-4 text-gray-500 dark:border-slate-700 dark:text-slate-500">
          {SAP_REFERENCE_FOOTER}
        </p>
      </section>
    );
  }

  if (!context) {
    return null;
  }

  const remainder = matches.length - visible.length;

  const governanceHeader = (
    <header className="space-y-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
        {isEvidencePurpose ? "SAP Reference Context" : "SAP data & governance context"}
      </h3>
      <p className="text-[11px] leading-snug text-gray-600 dark:text-slate-400">
        {isEvidencePurpose ? EVIDENCE_HELPER : HELPER_COPY}
      </p>
    </header>
  );

  const intakeHeader = (
    <header className="space-y-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-amber-900/85 dark:text-amber-200/85">
        {isEvidencePurpose ? "SAP intake signals" : "SAP intake detail needed"}
      </h3>
      <p className="text-[11px] leading-snug text-gray-600 dark:text-slate-400">
        {SAP_INTAKE_HELPER}
      </p>
    </header>
  );

  const matchList = (
    <div className="space-y-2.5">
      {visible.map((m, i) => (
        <MatchBlock
          key={`${m.matchType}-${i}-${m.matchedText?.slice(0, 72) ?? ""}`}
          layout={matchLayout}
          m={m}
        />
      ))}
    </div>
  );

  const showAllControl =
    matches.length > INITIAL_SHOW ? (
      <button
        type="button"
        onClick={() => setShowAll((v) => !v)}
        className="text-[11px] font-semibold text-cortex-blue hover:underline dark:text-emerald-300"
      >
        {showAll
          ? "Show less"
          : remainder > 0
            ? `Show all (+${remainder} more)`
            : "Show all"}
      </button>
    ) : null;

  const footer = (
    <p className="border-t border-gray-200/90 pt-2 text-[11px] leading-4 text-gray-500 dark:border-slate-700 dark:text-slate-500">
      {SAP_REFERENCE_FOOTER}
    </p>
  );

  const useRailChrome = variant === "embedded" || isEvidencePurpose;

  if (useRailChrome) {
    return (
      <div className="space-y-3">
        {isSapIntentOnly ? intakeHeader : governanceHeader}
        {!isSapIntentOnly ? matchList : null}
        {!isSapIntentOnly ? showAllControl : null}
        {!isEvidencePurpose && reviewerGuidance ? (
          <ReviewerGuidanceBlock guidance={reviewerGuidance} />
        ) : null}
        {footer}
      </div>
    );
  }

  return (
    <section className="rounded-md border border-gray-200 bg-gray-50/80 px-3 py-2.5 dark:border-slate-700 dark:bg-slate-900/50">
      {isSapIntentOnly ? intakeHeader : governanceHeader}
      {isSapIntentOnly ? null : <div className="mt-2">{matchList}</div>}
      {!isSapIntentOnly && showAllControl ? (
        <div className="mt-2">{showAllControl}</div>
      ) : null}
      {reviewerGuidance ? (
        <div className="mt-3">
          <ReviewerGuidanceBlock guidance={reviewerGuidance} />
        </div>
      ) : null}
      <div className="mt-3">{footer}</div>
    </section>
  );
}
