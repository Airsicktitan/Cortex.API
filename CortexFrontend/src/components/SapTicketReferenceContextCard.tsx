import { useEffect, useMemo, useState, type ReactNode } from "react";
import type {
  SapTicketReferenceContext,
  SapTicketReferenceMatch,
} from "../types/sapTicketReference";
import { formatDisplayValue } from "../utils/presentation";
import {
  buildSapReviewerGuidance,
  type SapReviewerGuidance,
} from "../utils/sapReviewerGuidance";

const HELPER_COPY =
  "Detected from SAP reference metadata stored in Cortex.";
const NO_LIVE_SAP_COPY =
  "Cortex is using stored SAP reference metadata only. No live SAP lookup was performed.";

const INITIAL_SHOW = 5;

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
    className={`inline-flex max-w-full rounded-full border px-2 py-0.5 text-[10px] font-semibold leading-tight ${pillClass(kind)}`}
  >
    {children}
  </span>
);

function MatchBlock({ m }: { m: SapTicketReferenceMatch }) {
  const isTable = m.matchType === "Table";
  const title = isTable
    ? formatDisplayValue(m.tableName)
    : formatDisplayValue(m.fieldName);

  const secondaryLine = isTable
    ? m.tableDescription?.trim() || null
    : m.tableName
      ? `Field on ${m.tableName}`
      : "Field";

  const fieldDetailLine =
    !isTable && m.fieldDescription?.trim()
      ? m.fieldDescription
      : !isTable && !m.fieldDescription && m.tableDescription?.trim()
        ? m.tableDescription
        : null;

  const moduleLineParts = [
    m.module ? `Module: ${m.module}` : null,
    m.businessObject ? `Business object: ${m.businessObject}` : null,
  ].filter(Boolean);

  const confidenceLabel =
    m.confidence === "High"
      ? "High confidence"
      : m.confidence === "Medium"
        ? "Medium confidence"
        : "Low confidence";

  return (
    <div className="rounded-lg border border-gray-200/95 bg-white/95 px-3 py-2.5 shadow-sm dark:border-slate-700 dark:bg-slate-900/45">
      <p className="text-sm font-semibold leading-snug text-gray-900 dark:text-slate-100">
        {title}
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
            {m.isCustom ? <Pill kind="custom">Custom field</Pill> : null}
          </>
        )}
        <Pill kind="confidence">{confidenceLabel}</Pill>
      </div>

      {!isTable &&
      fieldDetailLine &&
      fieldDetailLine !== secondaryLine ? (
        <p className="mt-2 text-[11px] leading-snug text-gray-600 dark:text-slate-400">
          {fieldDetailLine}
        </p>
      ) : null}

      {isTable && moduleLineParts.length > 0 ? (
        <p className="mt-2 text-[11px] leading-snug text-gray-600 dark:text-slate-400">
          <span className="font-medium text-gray-500 dark:text-slate-500">
            Metadata:{" "}
          </span>
          {moduleLineParts.join(" · ")}
        </p>
      ) : null}

      <p className="mt-2 text-[10px] leading-snug text-gray-500 dark:text-slate-500">
        Source: {formatDisplayValue(m.sourceName)}
      </p>
    </div>
  );
}

function ReviewerGuidanceBlock({ guidance }: { guidance: SapReviewerGuidance }) {
  const list = (items: string[]) => (
    <ul className="mt-1.5 list-outside list-disc space-y-1 pl-4 text-[11px] leading-snug text-gray-700 dark:text-slate-300">
      {items.map((line) => (
        <li key={line}>{line}</li>
      ))}
    </ul>
  );

  return (
    <section
      className="border-t border-gray-200/90 pt-3 dark:border-slate-700"
      aria-label="SAP reviewer guidance"
    >
      <h4 className="text-[11px] font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
        Reviewer guidance
      </h4>
      <p className="mt-1 text-[10px] leading-snug text-gray-500 dark:text-slate-500">
        Suggested from stored SAP reference metadata. Cortex did not perform a live SAP lookup.
      </p>

      {guidance.summaryLines.length > 0 ? (
        <div className="mt-3">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-500">
            What Cortex inferred
          </p>
          {list(guidance.summaryLines)}
        </div>
      ) : null}

      {guidance.questions.length > 0 ? (
        <div className="mt-3">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-500">
            Questions to confirm
          </p>
          {list(guidance.questions)}
        </div>
      ) : null}

      {guidance.investigationPaths.length > 0 ? (
        <div className="mt-3">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-500">
            Suggested investigation path
          </p>
          {list(guidance.investigationPaths)}
        </div>
      ) : null}

      {guidance.ownershipHints.length > 0 ? (
        <div className="mt-3">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-500">
            Ownership hints
          </p>
          {list(guidance.ownershipHints)}
        </div>
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
}: {
  context: SapTicketReferenceContext | null;
  loading: boolean;
  loadError: boolean;
  /** `embedded`: no outer card border; for Cortex SAP tab. */
  variant?: SapTicketReferenceContextCardVariant;
}) {
  const [showAll, setShowAll] = useState(false);

  useEffect(() => {
    setShowAll(false);
  }, [context?.ticketId]);

  const matches = context?.matches ?? [];
  const reviewerGuidance = useMemo(
    () => (variant === "embedded" ? buildSapReviewerGuidance(matches) : null),
    [matches, variant],
  );
  const visible = useMemo(() => {
    if (showAll || matches.length <= INITIAL_SHOW) {
      return matches;
    }
    return matches.slice(0, INITIAL_SHOW);
  }, [matches, showAll]);

  if (loading) {
    if (variant === "embedded") {
      return (
        <p className="text-[11px] text-gray-500 dark:text-slate-500">
          Loading SAP reference context…
        </p>
      );
    }
    return null;
  }

  const errorBlock = (
    <p className="text-[11px] text-gray-600 dark:text-slate-400">
      Unable to load SAP context.
    </p>
  );

  if (loadError) {
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

  if (!context || matches.length === 0) {
    return null;
  }

  const remainder = matches.length - visible.length;

  const header = (
    <header className="space-y-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
        SAP context
      </h3>
      <p className="text-[11px] leading-snug text-gray-600 dark:text-slate-400">
        {HELPER_COPY}
      </p>
    </header>
  );

  const matchList = (
    <div className="space-y-2.5">
      {visible.map((m, i) => (
        <MatchBlock
          key={`${m.matchType}-${m.tableName ?? ""}-${m.fieldName ?? ""}-${m.sourceId ?? ""}-${i}`}
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
    <p className="border-t border-gray-200/90 pt-2 text-[10px] leading-snug text-gray-500 dark:border-slate-700 dark:text-slate-500">
      {NO_LIVE_SAP_COPY}
    </p>
  );

  if (variant === "embedded") {
    return (
      <div className="space-y-3">
        {header}
        {matchList}
        {showAllControl}
        {reviewerGuidance ? (
          <ReviewerGuidanceBlock guidance={reviewerGuidance} />
        ) : null}
        {footer}
      </div>
    );
  }

  return (
    <section className="rounded-md border border-gray-200 bg-gray-50/80 px-3 py-2.5 dark:border-slate-700 dark:bg-slate-900/50">
      {header}
      <div className="mt-2">{matchList}</div>
      {showAllControl ? <div className="mt-2">{showAllControl}</div> : null}
      <div className="mt-3">{footer}</div>
    </section>
  );
}
