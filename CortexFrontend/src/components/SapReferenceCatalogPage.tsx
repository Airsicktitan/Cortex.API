import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import type { SapReferenceCatalogEntry } from "../types/sapReferenceCatalog";
import { getUserFacingErrorMessage } from "../services/api";
import { sapReferenceCatalogService } from "../services/sapReferenceCatalogService";
import { GOVERNANCE_ADVISORY_BOUNDARY } from "../utils/governanceAdvisoryCopy";
import { getSapSortKey, normalizeSearchText } from "../utils/catalogSearchRanking";
import {
  ConfigDetailCard,
  ConfigErrorBanner,
  ConfigPageBody,
  ConfigPageHeader,
  ConfigPageShell,
  ConfigSecondaryButton,
} from "./configurationAdminUi";

const API_AUDIENCE = "https://cortex-api";

function Chip({
  children,
  tone = "neutral",
}: {
  children: string;
  tone?: "neutral" | "category" | "muted" | "accent";
}) {
  const cls =
    tone === "category"
      ? "border-emerald-400/45 bg-emerald-100/70 text-emerald-950 dark:border-emerald-700/45 dark:bg-emerald-950/35 dark:text-emerald-100"
      : tone === "accent"
        ? "border-amber-400/45 bg-amber-100/70 text-amber-950 dark:border-amber-700/45 dark:bg-amber-950/35 dark:text-amber-100"
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

function businessContextLine(entry: SapReferenceCatalogEntry): string | null {
  const parts = [entry.module, entry.domain, entry.businessObject].filter((p) => p?.trim());
  if (parts.length === 0) {
    return null;
  }
  return parts.join(" · ");
}

function CatalogEntryCard({ entry }: { entry: SapReferenceCatalogEntry }) {
  const ctx = businessContextLine(entry);
  const isTable = entry.rowKind === "Table";

  return (
    <article className="rounded-xl border border-gray-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900/50">
      <div className="border-b border-gray-100 px-4 py-3 dark:border-slate-800">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="min-w-0">
            {isTable ? (
              <h3 className="font-mono text-base font-semibold text-gray-900 dark:text-slate-100">
                {entry.tableName}
              </h3>
            ) : (
              <h3 className="font-mono text-base font-semibold text-gray-900 dark:text-slate-100">
                {entry.tableName} / {entry.fieldName}
              </h3>
            )}
            <div className="mt-1.5 flex flex-wrap items-center gap-2">
              <Chip tone={isTable ? "category" : "neutral"}>{isTable ? "Table" : "Field"}</Chip>
              {!isTable && entry.isKey ? <Chip tone="muted">Key field</Chip> : null}
              {!isTable && entry.isRequired === true ? <Chip tone="muted">Required</Chip> : null}
              {(isTable ? entry.isCustomField : entry.likelyCustomSapField) ? (
                <Chip tone="accent">Likely custom SAP field</Chip>
              ) : null}
              {!entry.sourceIsEnabled ? <Chip tone="muted">Inactive catalog source</Chip> : null}
            </div>
          </div>
          <p className="text-[11px] text-slate-500 dark:text-slate-400">
            Updated {formatUtc(entry.updatedAtUtc ?? entry.createdAtUtc)}
          </p>
        </div>
        <p className="mt-2 text-[11px] text-slate-500 dark:text-slate-400">
          Source · {entry.sourceName} · {entry.sourceType}
        </p>
      </div>

      <div className="space-y-3 px-4 py-3">
        {ctx ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Business context
            </p>
            <p className="mt-1 text-sm text-gray-800 dark:text-slate-200">{ctx}</p>
          </div>
        ) : null}

        {isTable ? (
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Description
            </p>
            <p className="mt-1 text-sm leading-relaxed text-gray-800 dark:text-slate-200">
              {entry.tableDescription?.trim() || "—"}
            </p>
            <p className="mt-2 text-[11px] text-slate-500 dark:text-slate-400">
              Fields in stored catalog: {entry.fieldCount}
            </p>
          </div>
        ) : (
          <>
            <div>
              <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                Field description
              </p>
              <p className="mt-1 text-sm leading-relaxed text-gray-800 dark:text-slate-200">
                {entry.fieldDescription?.trim() || "—"}
              </p>
            </div>
            {entry.tableDescription?.trim() ? (
              <div>
                <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                  Table context
                </p>
                <p className="mt-1 text-sm leading-relaxed text-gray-800 dark:text-slate-200">
                  {entry.tableDescription.trim()}
                </p>
              </div>
            ) : null}
          </>
        )}
      </div>
    </article>
  );
}

export default function SapReferenceCatalogPage() {
  const { getAccessTokenSilently } = useAuth0();
  const getToken = useCallback(
    () =>
      getAccessTokenSilently({
        authorizationParams: { audience: API_AUDIENCE },
      }),
    [getAccessTokenSilently],
  );

  const [allEntries, setAllEntries] = useState<SapReferenceCatalogEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [tableFilter, setTableFilter] = useState("");
  const [contextFilter, setContextFilter] = useState("");

  const load = useCallback(async () => {
    setError(null);
    setLoading(true);
    try {
      const token = await getToken();
      const res = await sapReferenceCatalogService.list(token);
      setAllEntries(res.entries ?? []);
    } catch (e) {
      setAllEntries([]);
      setError(getUserFacingErrorMessage(e, "Unable to load the SAP reference catalog."));
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  useEffect(() => {
    void load();
  }, [load]);

  const tableOptions = useMemo(() => {
    const unique = new Set<string>();
    for (const e of allEntries) {
      if (e.tableName?.trim()) {
        unique.add(e.tableName.trim());
      }
    }
    return [...unique].sort((a, b) => a.localeCompare(b));
  }, [allEntries]);

  const contextOptions = useMemo(() => {
    const unique = new Set<string>();
    for (const e of allEntries) {
      const line = businessContextLine(e);
      if (line) {
        unique.add(line);
      }
    }
    return [...unique].sort((a, b) => a.localeCompare(b));
  }, [allEntries]);

  const filteredEntries = useMemo(() => {
    const qRaw = search.trim().toLowerCase();
    const qNorm = normalizeSearchText(search);
    const fromFilter = allEntries.filter((e) => {
      if (tableFilter && e.tableName !== tableFilter) {
        return false;
      }
      if (contextFilter) {
        const line = businessContextLine(e);
        if (line !== contextFilter) {
          return false;
        }
      }
      if (!qRaw) {
        return true;
      }
      const hay = [
        e.rowKind,
        e.tableName,
        e.fieldName,
        e.tableDescription,
        e.fieldDescription,
        e.businessObject,
        e.module,
        e.domain,
        e.sourceName,
        e.sourceType,
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();
      return hay.includes(qRaw);
    });

    if (!qNorm) {
      return fromFilter;
    }

    return [...fromFilter].sort((a, b) => {
      const ka = getSapSortKey(a, qNorm);
      const kb = getSapSortKey(b, qNorm);
      if (ka !== kb) {
        return ka - kb;
      }
      const rowOrder = (x: SapReferenceCatalogEntry) => (x.rowKind.toLowerCase() === "table" ? 0 : 1);
      const ro = rowOrder(a) - rowOrder(b);
      if (ro !== 0) {
        return ro;
      }
      const tt = a.tableName.localeCompare(b.tableName, undefined, { sensitivity: "base" });
      if (tt !== 0) {
        return tt;
      }
      const fa = a.fieldName ?? "";
      const fb = b.fieldName ?? "";
      return fa.localeCompare(fb, undefined, { sensitivity: "base" });
    });
  }, [allEntries, search, tableFilter, contextFilter]);

  const summaryMeta = (
    <div className="max-w-3xl space-y-2 text-xs leading-relaxed text-slate-600 dark:text-slate-400">
      <p>
        <span className="font-medium text-slate-700 dark:text-slate-300">{GOVERNANCE_ADVISORY_BOUNDARY}</span>
      </p>
      <p>
        This page shows stored reference metadata only. It is not a live SAP connection. Reviewer
        verification required.
      </p>
    </div>
  );

  return (
    <ConfigPageShell>
      <ConfigPageHeader
        title="SAP Reference Catalog"
        description="Stored SAP table and field metadata Cortex uses to support reviewer guidance."
        meta={summaryMeta}
        actions={
          <ConfigSecondaryButton type="button" onClick={() => void load()} disabled={loading}>
            Refresh
          </ConfigSecondaryButton>
        }
      />
      <ConfigPageBody>
        {error ? <ConfigErrorBanner>{error}</ConfigErrorBanner> : null}

        <div className="mb-6 grid gap-3 md:grid-cols-[1fr_minmax(140px,180px)_minmax(160px,220px)]">
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Search
            </label>
            <input
              type="search"
              value={search}
              onChange={(ev) => setSearch(ev.target.value)}
              placeholder="Search tables, fields, descriptions, domains, or business objects..."
              className="mt-1.5 w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 shadow-sm dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100"
            />
          </div>
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Table
            </label>
            <select
              value={tableFilter}
              onChange={(ev) => setTableFilter(ev.target.value)}
              className="mt-1.5 w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 shadow-sm dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100"
            >
              <option value="">All tables</option>
              {tableOptions.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Business context
            </label>
            <select
              value={contextFilter}
              onChange={(ev) => setContextFilter(ev.target.value)}
              className="mt-1.5 w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 shadow-sm dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100"
            >
              <option value="">All contexts</option>
              {contextOptions.map((c) => (
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
          <ConfigDetailCard title="Reference details">
            <p className="text-sm text-gray-700 dark:text-slate-300">No SAP reference entries found.</p>
            <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">
              SAP reference metadata is populated from stored catalogs. Use “Manage reference data” to add
              entries when your organization is ready.
            </p>
          </ConfigDetailCard>
        ) : filteredEntries.length === 0 ? (
          <ConfigDetailCard title="Reference details">
            <p className="text-sm text-gray-700 dark:text-slate-300">
              No SAP references match the current search.
            </p>
          </ConfigDetailCard>
        ) : (
          <div className="space-y-4">
            <p className="text-xs text-slate-500 dark:text-slate-400">
              Showing {filteredEntries.length} of {allEntries.length} catalog rows
            </p>
            {filteredEntries.map((entry, idx) => (
              <CatalogEntryCard
                key={`${entry.rowKind}:${entry.tableName}:${entry.fieldName ?? ""}:${entry.sourceName}:${idx}`}
                entry={entry}
              />
            ))}
          </div>
        )}
      </ConfigPageBody>
    </ConfigPageShell>
  );
}
