import type {
  SynitiKnowledgeContext,
  SynitiKnowledgeContextMatch,
} from "../types/synitiKnowledgeContext";

const EMPTY_COPY =
  "No Syniti knowledge context was found for this ticket yet.";
const FOOTER_HELPER =
  "Reference glossary entries only — advisory context, not operational guidance.";

function MatchSection({ m }: { m: SynitiKnowledgeContextMatch }) {
  return (
    <article className="rounded-lg border border-slate-400/45 bg-white px-3 py-2.5 shadow-sm ring-1 ring-slate-900/[0.05] dark:border-slate-500/55 dark:bg-slate-900/55 dark:ring-white/[0.06]">
      <p className="text-sm font-semibold leading-snug text-gray-900 dark:text-slate-100">
        {m.term}
      </p>
      <p className="mt-0.5 text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
        {m.category}
      </p>
      <div className="mt-2">
        <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
          Meaning
        </p>
        <p className="mt-0.5 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
          {m.shortDefinition.trim()}
        </p>
      </div>
      {m.businessMeaning?.trim() ? (
        <div className="mt-2">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Business context
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
          Why Cortex surfaced it
        </p>
        <p className="mt-0.5 text-[11px] leading-relaxed text-gray-700 dark:text-slate-300">
          {m.sourceReason.trim()}
        </p>
      </div>
      {m.relatedTermsPreview?.trim() ? (
        <div className="mt-2">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Related terms
          </p>
          <p className="mt-0.5 text-[11px] leading-relaxed text-gray-600 dark:text-slate-400">
            {m.relatedTermsPreview.trim()}
          </p>
        </div>
      ) : null}
      <p className="mt-2 text-[10px] font-semibold uppercase tracking-wide text-emerald-800/85 dark:text-emerald-300/90">
        {m.matchStrengthLabel.trim()}
      </p>
    </article>
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
          <p className="text-[11px] leading-snug text-gray-600 dark:text-slate-400">
            {EMPTY_COPY}
          </p>
        </header>
        <p className="mt-2 border-t border-gray-200/90 pt-2 text-[10px] leading-4 text-gray-500 dark:border-slate-700 dark:text-slate-500">
          {FOOTER_HELPER}
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
          Deterministic glossary matches from stored reference entries (advisory only).
        </p>
      </header>
      <div className="space-y-2.5">
        {matches.map((m, i) => (
          <MatchSection key={`${m.term}-${i}`} m={m} />
        ))}
      </div>
      <p className="border-t border-gray-200/90 pt-2 text-[10px] leading-4 text-gray-500 dark:border-slate-700 dark:text-slate-500">
        {FOOTER_HELPER}
      </p>
    </section>
  );
}
