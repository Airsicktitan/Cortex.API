import { useState } from "react";
import type {
  SynitiKnowledgeContext,
  SynitiKnowledgeContextMatch,
} from "../types/synitiKnowledgeContext";
import {
  GOVERNANCE_ADVISORY_BOUNDARY,
  GOVERNANCE_REVIEWER_VERIFICATION,
  SYNITI_MATCHED_CONCEPTS_INTRO,
} from "../utils/governanceAdvisoryCopy";

const EMPTY_COPY =
  "No Syniti knowledge context was found for this ticket yet — wording may not match the current glossary.";

function PrimaryConceptCard({ m }: { m: SynitiKnowledgeContextMatch }) {
  const guidance = m.reviewerGuidance?.trim() || m.shortDefinition.trim();
  const checks = m.suggestedReviewerChecks ?? [];
  const missing = m.missingContextQuestions ?? [];

  return (
    <article className="rounded-lg border border-slate-400/45 bg-white px-3 py-2.5 shadow-sm ring-1 ring-slate-900/[0.05] dark:border-slate-500/55 dark:bg-slate-900/55 dark:ring-white/[0.06]">
      <p className="text-[10px] font-semibold uppercase tracking-wide text-emerald-800/90 dark:text-emerald-300/90">
        Primary concept
      </p>
      <p className="mt-1 text-sm font-semibold leading-snug text-gray-900 dark:text-slate-100">
        {m.term}
      </p>
      <p className="mt-0.5 text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
        {m.category}
      </p>

      <div className="mt-2">
        <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
          Reviewer guidance
        </p>
        <p className="mt-0.5 text-[11px] leading-relaxed text-gray-800 dark:text-slate-200">{guidance}</p>
      </div>

      {m.shortDefinition.trim() && m.shortDefinition.trim() !== guidance ? (
        <div className="mt-2">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Overview
          </p>
          <p className="mt-0.5 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
            {m.shortDefinition.trim()}
          </p>
        </div>
      ) : null}

      {checks.length > 0 ? (
        <div className="mt-2">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Suggested reviewer checks
          </p>
          <ul className="mt-1 list-outside list-disc space-y-0.5 pl-4 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
            {checks.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
        </div>
      ) : null}

      {missing.length > 0 ? (
        <div className="mt-2">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Missing details to confirm
          </p>
          <ul className="mt-1 list-outside list-disc space-y-0.5 pl-4 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
            {missing.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
        </div>
      ) : null}

      {m.businessMeaning?.trim() &&
      m.businessMeaning.trim() !== guidance &&
      m.businessMeaning.trim() !== m.shortDefinition.trim() ? (
        <div className="mt-2">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Additional business context
          </p>
          <p className="mt-0.5 text-[11px] leading-relaxed text-gray-600 dark:text-slate-400">
            {m.businessMeaning.trim()}
          </p>
        </div>
      ) : null}

      {m.technicalMeaning?.trim() ? (
        <div className="mt-2">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Technical note
          </p>
          <p className="mt-0.5 text-[11px] leading-relaxed text-gray-600 dark:text-slate-400">
            {m.technicalMeaning.trim()}
          </p>
        </div>
      ) : null}

      <div className="mt-2">
        <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
          Reference note
        </p>
        <p className="mt-0.5 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
          {m.sourceReason.trim()}
        </p>
      </div>

      {m.relatedTermsPreview?.trim() ? (
        <div className="mt-2">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Related concepts
          </p>
          <p className="mt-0.5 text-[11px] leading-relaxed text-gray-600 dark:text-slate-400">
            {m.relatedTermsPreview.trim()}
          </p>
        </div>
      ) : null}

      <p className="mt-2 text-[10px] text-slate-500 dark:text-slate-500">
        {m.matchStrengthLabel.trim()}
      </p>
    </article>
  );
}

function RelatedConceptExpandedBody({ m }: { m: SynitiKnowledgeContextMatch }) {
  const guidance = m.reviewerGuidance?.trim() || m.shortDefinition.trim();
  const checks = m.suggestedReviewerChecks ?? [];
  const missing = m.missingContextQuestions ?? [];

  return (
    <div className="space-y-2">
      <div>
        <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
          Reviewer guidance
        </p>
        <p className="mt-0.5 text-[11px] leading-relaxed text-gray-800 dark:text-slate-200">{guidance}</p>
      </div>
      {m.shortDefinition.trim() && m.shortDefinition.trim() !== guidance ? (
        <div>
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Overview
          </p>
          <p className="mt-0.5 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
            {m.shortDefinition.trim()}
          </p>
        </div>
      ) : null}
      {checks.length > 0 ? (
        <div>
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Suggested reviewer checks
          </p>
          <ul className="mt-1 list-outside list-disc space-y-0.5 pl-4 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
            {checks.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
        </div>
      ) : null}
      {missing.length > 0 ? (
        <div>
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Missing details to confirm
          </p>
          <ul className="mt-1 list-outside list-disc space-y-0.5 pl-4 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
            {missing.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
        </div>
      ) : null}
      <div>
        <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
          Reference note
        </p>
        <p className="mt-0.5 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
          {m.sourceReason.trim()}
        </p>
      </div>
      <p className="text-[10px] text-slate-500 dark:text-slate-500">{m.matchStrengthLabel.trim()}</p>
    </div>
  );
}

function RelatedConceptCompactRow({ m }: { m: SynitiKnowledgeContextMatch }) {
  const [open, setOpen] = useState(false);
  const rawBlurb = (m.shortDefinition?.trim() || m.reviewerGuidance?.trim() || "").replace(
    /\s+/g,
    " ",
  );
  const blurbPreview =
    rawBlurb.length > 90 ? `${rawBlurb.slice(0, 89).trim()}…` : rawBlurb;

  return (
    <div className="border-b border-slate-200/75 pb-2 last:border-b-0 last:pb-0 dark:border-slate-700/75">
      <div className="flex items-start gap-2">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
            <span className="text-[11px] font-semibold text-gray-900 dark:text-slate-100">
              {m.term}
            </span>
            <span className="text-[9px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-500">
              {m.category}
            </span>
          </div>
          {!open && blurbPreview ? (
            <p className="mt-0.5 line-clamp-2 text-[10px] leading-snug text-slate-600 dark:text-slate-400">
              {blurbPreview}
            </p>
          ) : null}
        </div>
        <button
          type="button"
          className="shrink-0 text-[10px] font-semibold leading-none text-cortex-blue hover:underline dark:text-emerald-300"
          aria-expanded={open}
          onClick={() => setOpen((v) => !v)}
        >
          {open ? "Less" : "Detail"}
        </button>
      </div>
      {open ? (
        <div className="mt-2 rounded-md border border-slate-200/90 bg-white/70 px-2 py-2 dark:border-slate-600/70 dark:bg-slate-900/45">
          <RelatedConceptExpandedBody m={m} />
        </div>
      ) : null}
    </div>
  );
}

export function SynitiKnowledgeContextCard({
  context,
  loading,
  loadError,
}: {
  context: SynitiKnowledgeContext | null;
  loading: boolean;
  loadError: boolean;
}) {
  const matches = context?.matches ?? [];
  const primary = matches[0];
  const related = matches.slice(1);

  if (loading) {
    return (
      <section className="rounded-md border border-gray-200/90 bg-gray-50/70 px-3 py-2.5 dark:border-slate-700 dark:bg-slate-900/40">
        <header className="space-y-1">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
            Syniti Knowledge Context
          </h3>
        </header>
        <p className="mt-2 text-[11px] text-gray-500 dark:text-slate-500">
          Loading Syniti knowledge context…
        </p>
      </section>
    );
  }

  if (loadError) {
    return (
      <section className="rounded-md border border-amber-200/90 bg-amber-50/60 px-3 py-2.5 dark:border-amber-800/55 dark:bg-amber-950/25">
        <header className="space-y-1">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
            Syniti Knowledge Context
          </h3>
        </header>
        <p className="mt-2 text-[11px] text-gray-600 dark:text-slate-400">
          Unable to load Syniti knowledge context.
        </p>
      </section>
    );
  }

  if (!context || matches.length === 0) {
    return (
      <section className="rounded-md border border-gray-200/90 bg-gray-50/70 px-3 py-2.5 dark:border-slate-700 dark:bg-slate-900/40">
        <header className="space-y-1">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
            Syniti Knowledge Context
          </h3>
          <p className="text-[11px] leading-snug text-gray-600 dark:text-slate-400">{EMPTY_COPY}</p>
        </header>
        <p className="mt-2 border-t border-gray-200/90 pt-2 text-[10px] leading-4 text-gray-500 dark:border-slate-700 dark:text-slate-500">
          {GOVERNANCE_REVIEWER_VERIFICATION}
        </p>
      </section>
    );
  }

  return (
    <section className="space-y-2.5">
      <header className="space-y-1">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
          Syniti Knowledge Context
        </h3>
        <p className="text-[11px] leading-snug text-gray-600 dark:text-slate-400">
          {SYNITI_MATCHED_CONCEPTS_INTRO}
        </p>
      </header>
      {primary ? <PrimaryConceptCard m={primary} /> : null}
      {related.length > 0 ? (
        <div className="rounded-md border border-slate-200/75 bg-slate-50/40 px-2.5 py-2 dark:border-slate-600/50 dark:bg-slate-900/25">
          <p className="mb-2 text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Related concepts
          </p>
          <div className="space-y-2">
            {related.map((m, i) => (
              <RelatedConceptCompactRow key={`${m.term}-${i}`} m={m} />
            ))}
          </div>
        </div>
      ) : null}
      <p className="border-t border-gray-200/90 pt-2 text-[10px] leading-4 text-gray-500 dark:border-slate-700 dark:text-slate-500">
        {GOVERNANCE_ADVISORY_BOUNDARY}
      </p>
    </section>
  );
}
