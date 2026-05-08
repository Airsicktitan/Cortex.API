import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import type { SynitiKnowledgeCatalogEntry } from "../types/synitiKnowledgeCatalog";
import { getUserFacingErrorMessage } from "../services/api";
import { synitiKnowledgeCatalogService } from "../services/synitiKnowledgeCatalogService";
import { GOVERNANCE_ADVISORY_BOUNDARY } from "../utils/governanceAdvisoryCopy";
import {
  ConfigDetailCard,
  ConfigErrorBanner,
  ConfigPageBody,
  ConfigPageHeader,
  ConfigPageShell,
  ConfigSecondaryButton,
} from "./configurationAdminUi";

const API_AUDIENCE = "https://cortex-api";

function splitDelimited(raw: string | null | undefined, pattern: RegExp): string[] {
  if (!raw?.trim()) {
    return [];
  }

  return raw
    .split(pattern)
    .map((s) => s.trim())
    .filter(Boolean);
}

function Chip({
  children,
  tone = "neutral",
}: {
  children: string;
  tone?: "neutral" | "category" | "muted";
}) {
  const cls =
    tone === "category"
      ? "border-emerald-400/45 bg-emerald-100/70 text-emerald-950 dark:border-emerald-700/45 dark:bg-emerald-950/35 dark:text-emerald-100"
      : tone === "muted"
        ? "border-slate-300/80 bg-slate-100/80 text-slate-700 dark:border-slate-600 dark:bg-slate-800/80 dark:text-slate-200"
        : "border-slate-300/80 bg-white text-slate-800 dark:border-slate-600 dark:bg-slate-900/60 dark:text-slate-100";

  return (
    <span
      className={`inline-flex max-w-full rounded-full border px-2 py-0.5 text-[11px] font-medium leading-snug ${cls}`}
    >
      {children}
    </span>
  );
}

function formatUtc(iso: string | null | undefined): string {
  if (!iso) {
    return "—";
  }

  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) {
    return "—";
  }

  return d.toLocaleString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function CatalogEntryCard({ entry }: { entry: SynitiKnowledgeCatalogEntry }) {
  const aliasChips = splitDelimited(entry.aliases, /[;\n]+/);
  const triggerChips = splitDelimited(entry.examplePhrases, /[;\n]+/);
  const relatedChips = splitDelimited(entry.relatedTerms, /[;\n]+/);

  return (
    <article className="rounded-xl border border-gray-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900/50">
      <div className="border-b border-gray-100 px-4 py-3 dark:border-slate-800">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="min-w-0">
            <h3 className="text-base font-semibold text-gray-900 dark:text-slate-100">
              {entry.term}
            </h3>
            <div className="mt-1.5 flex flex-wrap items-center gap-2">
              <Chip tone="category">{entry.category}</Chip>
              {!entry.sourceIsEnabled ? (
                <Chip tone="muted">Inactive catalog source</Chip>
              ) : null}
            </div>
          </div>
          <p className="text-[11px] text-slate-500 dark:text-slate-400">
            Updated {formatUtc(entry.updatedAtUtc ?? entry.createdAtUtc)}
          </p>
        </div>
        <p className="mt-2 text-[11px] text-slate-500 dark:text-slate-400">
          Catalog: {entry.sourceName} · {entry.sourceType}
        </p>
      </div>

      <div className="space-y-3 px-4 py-3">
        {aliasChips.length > 0 ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Aliases
            </p>
            <div className="mt-1.5 flex flex-wrap gap-1.5">
              {aliasChips.map((a) => (
                <Chip key={a}>{a}</Chip>
              ))}
            </div>
          </div>
        ) : null}

        {triggerChips.length > 0 ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Trigger phrases
            </p>
            <div className="mt-1.5 flex flex-wrap gap-1.5">
              {triggerChips.map((t) => (
                <Chip key={t} tone="muted">
                  {t}
                </Chip>
              ))}
            </div>
          </div>
        ) : null}

        <div>
          <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Overview
          </p>
          <p className="mt-1 text-sm leading-relaxed text-gray-800 dark:text-slate-200">
            {entry.shortDefinition}
          </p>
        </div>

        {entry.businessMeaning?.trim() ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Reviewer guidance
            </p>
            <p className="mt-1 text-sm leading-relaxed text-gray-800 dark:text-slate-200">
              {entry.businessMeaning.trim()}
            </p>
          </div>
        ) : null}

        {entry.suggestedReviewerChecks.length > 0 ? (
          <details className="rounded-lg border border-slate-200/90 bg-slate-50/50 px-3 py-2 dark:border-slate-700 dark:bg-slate-800/40">
            <summary className="cursor-pointer text-[11px] font-semibold text-slate-700 dark:text-slate-200">
              Suggested reviewer checks ({entry.suggestedReviewerChecks.length})
            </summary>
            <ul className="mt-2 list-outside list-disc space-y-1 pl-4 text-[13px] leading-relaxed text-gray-800 dark:text-slate-200">
              {entry.suggestedReviewerChecks.map((line) => (
                <li key={line}>{line}</li>
              ))}
            </ul>
          </details>
        ) : null}

        {entry.missingContextQuestions.length > 0 ? (
          <details className="rounded-lg border border-slate-200/90 bg-slate-50/50 px-3 py-2 dark:border-slate-700 dark:bg-slate-800/40">
            <summary className="cursor-pointer text-[11px] font-semibold text-slate-700 dark:text-slate-200">
              Missing details to confirm ({entry.missingContextQuestions.length})
            </summary>
            <ul className="mt-2 list-outside list-disc space-y-1 pl-4 text-[13px] leading-relaxed text-gray-800 dark:text-slate-200">
              {entry.missingContextQuestions.map((line) => (
                <li key={line}>{line}</li>
              ))}
            </ul>
          </details>
        ) : null}

        {relatedChips.length > 0 ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Related concepts
            </p>
            <div className="mt-1.5 flex flex-wrap gap-1.5">
              {relatedChips.map((r) => (
                <Chip key={r} tone="muted">
                  {r}
                </Chip>
              ))}
            </div>
          </div>
        ) : null}

        {entry.technicalMeaning?.trim() ? (
          <details className="rounded-lg border border-dashed border-slate-300/90 px-3 py-2 dark:border-slate-600">
            <summary className="cursor-pointer text-[11px] font-semibold text-slate-600 dark:text-slate-300">
              Additional technical note
            </summary>
            <p className="mt-2 text-xs leading-relaxed text-slate-600 dark:text-slate-400">
              {entry.technicalMeaning.trim()}
            </p>
          </details>
        ) : null}
      </div>
    </article>
  );
}

export default function SynitiKnowledgeCatalogPage({
  onOpenSapReference,
}: {
  onOpenSapReference?: () => void;
}) {
  const { getAccessTokenSilently } = useAuth0();
  const getToken = useCallback(
    () =>
      getAccessTokenSilently({
        authorizationParams: { audience: API_AUDIENCE },
      }),
    [getAccessTokenSilently],
  );

  const [allEntries, setAllEntries] = useState<SynitiKnowledgeCatalogEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("");

  const load = useCallback(async () => {
    setError(null);
    setLoading(true);
    try {
      const token = await getToken();
      const res = await synitiKnowledgeCatalogService.list(token);
      setAllEntries(res.entries ?? []);
    } catch (e) {
      setAllEntries([]);
      setError(getUserFacingErrorMessage(e, "Unable to load the Syniti knowledge catalog."));
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  useEffect(() => {
    void load();
  }, [load]);

  const categoryOptions = useMemo(() => {
    const unique = new Set<string>();
    for (const e of allEntries) {
      if (e.category?.trim()) {
        unique.add(e.category.trim());
      }
    }

    return [...unique].sort((a, b) => a.localeCompare(b));
  }, [allEntries]);

  const filteredEntries = useMemo(() => {
    const q = search.trim().toLowerCase();
    return allEntries.filter((e) => {
      if (category && e.category !== category) {
        return false;
      }

      if (!q) {
        return true;
      }

      const hay = [
        e.term,
        e.category,
        e.shortDefinition,
        e.businessMeaning,
        e.aliases,
        e.examplePhrases,
        e.relatedTerms,
        e.suggestedReviewerChecks.join(" "),
        e.missingContextQuestions.join(" "),
        e.sourceName,
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();

      return hay.includes(q);
    });
  }, [allEntries, search, category]);

  const boundaryMeta = (
    <p className="max-w-3xl text-xs leading-relaxed text-slate-600 dark:text-slate-400">
      Cortex uses stored reference catalogs for advisory reviewer guidance. This page shows the
      Syniti and data-governance concepts Cortex can recognize in ticket text.{" "}
      <span className="font-medium text-slate-700 dark:text-slate-300">{GOVERNANCE_ADVISORY_BOUNDARY}</span>
    </p>
  );

  return (
    <ConfigPageShell>
      <ConfigPageHeader
        title="Syniti Knowledge Catalog"
        description="Stored advisory concepts Cortex uses to support reviewer guidance."
        meta={boundaryMeta}
        actions={
          <ConfigSecondaryButton type="button" onClick={() => void load()} disabled={loading}>
            Refresh
          </ConfigSecondaryButton>
        }
      />
      <ConfigPageBody>
        {error ? <ConfigErrorBanner>{error}</ConfigErrorBanner> : null}

        <div className="mb-6 rounded-xl border border-slate-200/90 bg-slate-50/60 p-4 dark:border-slate-700 dark:bg-slate-800/35">
          <p className="text-sm text-gray-800 dark:text-slate-200">
            SAP table and field reference metadata is managed separately.{" "}
            {onOpenSapReference ? (
              <button
                type="button"
                className="font-semibold text-cortex-blue hover:underline dark:text-emerald-300"
                onClick={onOpenSapReference}
              >
                Open SAP reference
              </button>
            ) : null}
          </p>
        </div>

        <div className="mb-6 grid gap-3 md:grid-cols-[1fr_minmax(160px,220px)]">
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Search
            </label>
            <input
              type="search"
              value={search}
              onChange={(ev) => setSearch(ev.target.value)}
              placeholder="Search concepts, aliases, categories, or reviewer guidance..."
              className="mt-1.5 w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 shadow-sm dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100"
            />
          </div>
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Category
            </label>
            <select
              value={category}
              onChange={(ev) => setCategory(ev.target.value)}
              className="mt-1.5 w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 shadow-sm dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100"
            >
              <option value="">All categories</option>
              {categoryOptions.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </div>
        </div>

        {loading ? (
          <p className="py-12 text-center text-sm text-slate-500 dark:text-slate-400">
            Loading catalog…
          </p>
        ) : allEntries.length === 0 ? (
          <ConfigDetailCard title="No entries yet">
            <p className="text-sm text-gray-700 dark:text-slate-300">
              No Syniti knowledge entries found.
            </p>
            <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">
              Add seed data or configure reference catalogs to enable advisory guidance.
            </p>
          </ConfigDetailCard>
        ) : filteredEntries.length === 0 ? (
          <ConfigDetailCard title="No matches">
            <p className="text-sm text-gray-700 dark:text-slate-300">
              No concepts match the current search.
            </p>
          </ConfigDetailCard>
        ) : (
          <div className="space-y-4">
            <p className="text-xs text-slate-500 dark:text-slate-400">
              Showing {filteredEntries.length} of {allEntries.length} concepts
            </p>
            {filteredEntries.map((entry, idx) => (
              <CatalogEntryCard
                key={`${entry.sourceName}:${entry.term}:${idx}`}
                entry={entry}
              />
            ))}
          </div>
        )}
      </ConfigPageBody>
    </ConfigPageShell>
  );
}
