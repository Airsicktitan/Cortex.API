import type { SapDecisionAssist } from "../utils/sapDecisionAssist";

function BulletList({ items }: { items: string[] }) {
  if (!items.length) {
    return null;
  }
  return (
    <ul className="mt-1 list-outside list-disc space-y-1 pl-3.5 text-xs leading-5 text-gray-700 dark:text-slate-300">
      {items.map((line, i) => (
        <li key={`${i}-${line.slice(0, 40)}`}>{line}</li>
      ))}
    </ul>
  );
}

export function SapDecisionAssistCard({ assist }: { assist: SapDecisionAssist }) {
  const hasContent =
    assist.reviewReadinessLines.length > 0 ||
    assist.dataContextLines.length > 0 ||
    assist.beforeApprovalLines.length > 0 ||
    assist.governanceConcernLines.length > 0 ||
    assist.reviewerFocusLines.length > 0;

  if (!hasContent) {
    return null;
  }

  return (
    <div
      className="space-y-3 rounded-md border border-amber-200/70 bg-amber-50/40 p-4 dark:border-amber-900/40 dark:bg-amber-950/20"
      aria-label="SAP decision assist"
    >
      <div>
        <h3 className="text-[11px] font-semibold uppercase tracking-wide text-amber-900/90 dark:text-amber-200/90">
          SAP / data governance assist
        </h3>
        <p className="mt-1 text-[11px] leading-4 text-amber-950/85 dark:text-amber-100/75">
          Advisory SAP reference from the stored Cortex catalog. Does not change routing, owners, or
          approvals.
        </p>
      </div>

      {assist.reviewReadinessLines.length > 0 ? (
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-wide text-amber-900/80 dark:text-amber-200/85">
            Review readiness
          </p>
          <BulletList items={assist.reviewReadinessLines} />
        </div>
      ) : null}

      {assist.dataContextLines.length > 0 ? (
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-wide text-amber-900/80 dark:text-amber-200/85">
            Data context
          </p>
          <BulletList items={assist.dataContextLines} />
        </div>
      ) : null}

      {assist.beforeApprovalLines.length > 0 ? (
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-wide text-amber-900/80 dark:text-amber-200/85">
            Before approval
          </p>
          <BulletList items={assist.beforeApprovalLines} />
        </div>
      ) : null}

      {assist.governanceConcernLines.length > 0 ? (
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-wide text-amber-900/80 dark:text-amber-200/85">
            Potential governance impact
          </p>
          <BulletList items={assist.governanceConcernLines} />
        </div>
      ) : null}

      {assist.reviewerFocusLines.length > 0 ? (
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-wide text-amber-900/80 dark:text-amber-200/85">
            Suggested ownership / focus
          </p>
          <BulletList items={assist.reviewerFocusLines} />
        </div>
      ) : null}
    </div>
  );
}
