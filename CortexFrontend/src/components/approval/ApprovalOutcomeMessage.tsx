import type { Ticket } from "../../types/ticket";

export type ApprovalMessageAudience = "requester" | "reviewer";
export type ApprovalOutcomeVariant = "default" | "modalBanner";

type Props = {
  ticket: Ticket;
  /** Modal uses a tighter layout; default is for any full-width context. */
  variant?: ApprovalOutcomeVariant;
  /** Reviewers see action-oriented copy; requesters see outcome-focused copy. */
  audience?: ApprovalMessageAudience;
};

function reasonBlock(
  variant: ApprovalOutcomeVariant,
  tone: "amber" | "red",
  title: string,
  body: string,
) {
  const toneBox =
    tone === "red"
      ? "border-red-200/80 dark:border-red-900/40"
      : "border-amber-200/80 dark:border-amber-800/60";
  const box =
    variant === "modalBanner"
      ? `mt-2 rounded border ${toneBox} bg-white/90 p-2 dark:bg-slate-900/60`
      : `mt-3 rounded-md border ${toneBox} bg-white/80 p-3 dark:bg-slate-900/50`;
  const titleClass =
    variant === "modalBanner"
      ? "text-[10px] font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400"
      : tone === "red"
        ? "text-xs font-semibold uppercase tracking-wide text-red-800 dark:text-red-200/90"
        : "text-xs font-semibold uppercase tracking-wide text-amber-900/80 dark:text-amber-200/90";
  const bodyClass =
    variant === "modalBanner"
      ? "mt-1 whitespace-pre-wrap text-xs text-gray-800 dark:text-slate-200"
      : "mt-1 whitespace-pre-wrap text-sm text-gray-800 dark:text-slate-200";

  return (
    <div className={box}>
      <p className={titleClass}>{title}</p>
      <p className={bodyClass}>{body}</p>
    </div>
  );
}

/**
 * Intake / approval outcome copy. Use `modalBanner` + `audience` in Ticket Modal to avoid
 * competing with review actions and Cortex Decision.
 */
export function ApprovalOutcomeMessage({
  ticket,
  variant = "default",
  audience = "requester",
}: Props) {
  if (!ticket.id) {
    return null;
  }

  const approvalStatus = ticket.approvalStatus ?? "Approved";

  const isCompact = variant === "modalBanner";
  const wrap = isCompact
    ? "rounded-md border px-3 py-2 text-sm"
    : "rounded-lg border px-4 py-3 text-sm";
  const titleClass = isCompact ? "font-medium leading-snug" : "font-medium";
  const subClass = isCompact
    ? "mt-1 text-xs leading-snug opacity-90"
    : "mt-1 text-xs leading-snug";

  switch (approvalStatus) {
    case "PendingApproval":
      if (audience === "reviewer") {
        return (
          <div
            className={`${wrap} border-amber-200/80 bg-amber-50/70 text-amber-950 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-100`}
          >
            <p className={isCompact ? "text-xs leading-snug" : titleClass}>
              Review this submission before it enters active boards.
            </p>
          </div>
        );
      }
      return (
        <div
          className={`${wrap} border-amber-200 bg-amber-50/90 dark:border-amber-800 dark:bg-amber-950/35`}
        >
          <p className={`${titleClass} text-amber-950 dark:text-amber-100`}>
            {isCompact ? "Awaiting approval" : "Waiting for review"}
          </p>
          {!isCompact ? (
            <p className={`${subClass} text-amber-900/85 dark:text-amber-200/90`}>
              Your request is in the Approval Queue. It is not active work until a
              reviewer approves it.
            </p>
          ) : (
            <p className={`${subClass} text-amber-900/85 dark:text-amber-200/90`}>
              Your request is not active work until a reviewer approves it.
            </p>
          )}
        </div>
      );
    case "Approved":
      return (
        <div
          className={`${wrap} border-emerald-200 bg-emerald-50/90 dark:border-emerald-800 dark:bg-emerald-950/35`}
        >
          <p className={`${titleClass} text-emerald-950 dark:text-emerald-100`}>
            Approved and now part of active work.
          </p>
        </div>
      );
    case "NeedsMoreInfo":
      return (
        <div
          className={`${wrap} border-amber-200 bg-amber-50/90 dark:border-amber-800 dark:bg-amber-950/35`}
        >
          <p className={`${titleClass} text-amber-950 dark:text-amber-100`}>
            More information is required before this request can be approved.
          </p>
          {ticket.returnReason?.trim()
            ? reasonBlock(
                variant,
                "amber",
                "More information requested",
                ticket.returnReason.trim(),
              )
            : null}
        </div>
      );
    case "Rejected":
      return (
        <div
          className={`${wrap} border-red-200 bg-red-50/90 dark:border-red-900/50 dark:bg-red-950/35`}
        >
          <p className={`${titleClass} text-red-900 dark:text-red-100`}>
            This request was not approved.
          </p>
          {ticket.rejectionReason?.trim()
            ? reasonBlock(
                variant,
                "red",
                "Rejection reason",
                ticket.rejectionReason.trim(),
              )
            : null}
        </div>
      );
    default:
      return null;
  }
}
