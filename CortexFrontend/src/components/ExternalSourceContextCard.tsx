import type { TicketExternalSourceContextItem } from "../types/integrations";
import { formatDisplayDateTime, formatDisplayValue } from "../utils/presentation";

const DISCLAIMER =
  "Created from an external work item. Cortex does not update the source system automatically.";

function ContextRows({ ctx }: { ctx: TicketExternalSourceContextItem }) {
  const statusParts = [
    ctx.externalStatus ? `Status: ${ctx.externalStatus}` : null,
    ctx.externalPriority ? `Priority: ${ctx.externalPriority}` : null,
  ].filter(Boolean);

  const peopleParts = [
    ctx.requester ? `Requester: ${ctx.requester}` : null,
    ctx.assignedTo ? `Assigned: ${ctx.assignedTo}` : null,
  ].filter(Boolean);

  const extraParts = [
    ctx.department ? `Dept: ${ctx.department}` : null,
    ctx.category ? `Category: ${ctx.category}` : null,
  ].filter(Boolean);

  return (
    <div className="mt-2 space-y-1.5 text-xs text-gray-700 dark:text-slate-300">
      <p>
        <span className="font-medium text-gray-600 dark:text-slate-400">
          Source system:{" "}
        </span>
        {formatDisplayValue(ctx.provider)}
      </p>
      <p>
        <span className="font-medium text-gray-600 dark:text-slate-400">
          Source:{" "}
        </span>
        {formatDisplayValue(ctx.sourceName)}
      </p>
      <p>
        <span className="font-medium text-gray-600 dark:text-slate-400">
          External item:{" "}
        </span>
        {formatDisplayValue(ctx.externalItemId)}
      </p>
      {ctx.externalTitle ? (
        <p className="text-[11px] text-gray-600 dark:text-slate-400">
          <span className="font-medium text-gray-600 dark:text-slate-400">
            Title:{" "}
          </span>
          {ctx.externalTitle}
        </p>
      ) : null}
      {statusParts.length > 0 ? (
        <p className="text-[11px]">{statusParts.join(" · ")}</p>
      ) : null}
      {peopleParts.length > 0 ? (
        <p className="text-[11px]">{peopleParts.join(" · ")}</p>
      ) : null}
      {extraParts.length > 0 ? (
        <p className="text-[11px]">{extraParts.join(" · ")}</p>
      ) : null}
      <p className="text-[11px]">
        <span className="font-medium text-gray-600 dark:text-slate-400">
          Last seen:{" "}
        </span>
        {formatDisplayDateTime(ctx.lastSeenUtc)}
      </p>
      {ctx.lastModifiedUtc ? (
        <p className="text-[11px] text-gray-600 dark:text-slate-400">
          <span className="font-medium">Source modified: </span>
          {formatDisplayDateTime(ctx.lastModifiedUtc)}
        </p>
      ) : null}
      <div className="pt-1">
        {ctx.externalUrl ? (
          <a
            href={ctx.externalUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="font-semibold text-cortex-blue hover:underline dark:text-emerald-300"
          >
            Open source item
          </a>
        ) : (
          <span className="text-gray-500 dark:text-slate-500">
            No source link available.
          </span>
        )}
      </div>
    </div>
  );
}

export function ExternalSourceContextCard({
  contexts,
  loading,
  loadError,
}: {
  contexts: TicketExternalSourceContextItem[];
  loading: boolean;
  loadError: boolean;
}) {
  if (loading) {
    return (
      <section
        className="rounded-md border border-gray-200 bg-gray-50/80 px-3 py-2 dark:border-slate-700 dark:bg-slate-900/50"
        aria-busy="true"
        aria-label="Loading external source context"
      >
        <p className="text-[11px] text-gray-500 dark:text-slate-400">
          Loading source context…
        </p>
      </section>
    );
  }

  if (loadError) {
    return (
      <section className="rounded-md border border-amber-200/80 bg-amber-50/60 px-3 py-2 dark:border-amber-900/50 dark:bg-amber-950/30">
        <p className="text-[11px] text-amber-900 dark:text-amber-100/90">
          Source context could not be loaded.
        </p>
      </section>
    );
  }

  if (contexts.length === 0) {
    return null;
  }

  return (
    <section className="space-y-2">
      {contexts.map((ctx, index) => (
        <div
          key={`${ctx.externalWorkItemId}-${index}`}
          className="rounded-md border border-gray-200 bg-gray-50/80 px-3 py-2.5 dark:border-slate-700 dark:bg-slate-900/50"
        >
          <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
            Source context
            {contexts.length > 1 ? ` (${index + 1}/${contexts.length})` : null}
          </h3>
          <p className="mt-1.5 text-[11px] leading-snug text-gray-600 dark:text-slate-400">
            {DISCLAIMER}
          </p>
          {ctx.message ? (
            <p className="mt-1 text-[11px] text-gray-600 dark:text-slate-400">
              {ctx.message}
            </p>
          ) : null}
          <ContextRows ctx={ctx} />
        </div>
      ))}
    </section>
  );
}
