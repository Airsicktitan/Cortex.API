import { useEffect, useMemo, useState } from "react";
import TicketCard from "./TicketCard";
import { TicketGridSkeleton } from "./LoadingSkeletons";
import type { ThemeMode } from "../theme";
import type { Ticket } from "../types/ticket";
import { formatApprovalQueueLastUpdatedLabel } from "../utils/approvalQueueLastUpdated";
import toast from "react-hot-toast";
import { canReviewApprovalQueue } from "../utils/role";

const MAX_REASON = 2000;

type ApprovalQueuePageProps = {
  theme: ThemeMode;
  isAuthenticated: boolean;
  bootstrapComplete: boolean;
  needsConsent: boolean;
  authRoles: string[] | undefined;
  openTicketById: (ticketId: string, providedToken?: string) => Promise<void>;
  tickets: Ticket[];
  loading: boolean;
  error: string | null;
  silentRefreshing: boolean;
  lastSuccessfulRefreshAt: number | null;
  onRefresh: () => Promise<void> | void;
  /**
   * Approve/return/reject handlers shared with the Ticket Modal flow so the
   * approval queue list, board, and selected ticket all update from the same
   * code path. Each handler returns the updated ticket on success.
   */
  onApprove: (ticketId: string) => Promise<Ticket | null>;
  onReturnForDetail: (ticketId: string, reason: string) => Promise<Ticket | null>;
  onReject: (ticketId: string, reason: string) => Promise<Ticket | null>;
};

export default function ApprovalQueuePage({
  theme,
  isAuthenticated,
  bootstrapComplete,
  needsConsent,
  authRoles,
  openTicketById,
  tickets,
  loading,
  error,
  silentRefreshing,
  lastSuccessfulRefreshAt,
  onRefresh,
  onApprove,
  onReturnForDetail,
  onReject,
}: ApprovalQueuePageProps) {
  const [searchQuery, setSearchQuery] = useState("");
  const [reasonModal, setReasonModal] = useState<
    | { ticketId: string; mode: "return" | "reject" }
    | null
  >(null);
  const [reasonDraft, setReasonDraft] = useState("");
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [relativeTimeTick, setRelativeTimeTick] = useState(0);

  const canReview = canReviewApprovalQueue(authRoles);

  useEffect(() => {
    if (loading || lastSuccessfulRefreshAt === null) {
      return;
    }
    const id = window.setInterval(() => {
      setRelativeTimeTick((value) => value + 1);
    }, 30000);
    return () => window.clearInterval(id);
  }, [loading, lastSuccessfulRefreshAt]);

  const lastUpdatedDisplay = useMemo(() => {
    if (lastSuccessfulRefreshAt === null) {
      return null;
    }
    return formatApprovalQueueLastUpdatedLabel(lastSuccessfulRefreshAt);
  }, [lastSuccessfulRefreshAt, relativeTimeTick]);

  const normalizedSearch = searchQuery.trim().toLowerCase();
  const filteredTickets = useMemo(() => {
    if (!normalizedSearch) {
      return tickets;
    }
    return tickets.filter((ticket) => {
      const haystack = [
        ticket.id,
        ticket.title,
        ticket.description,
        ticket.createdByDisplayName,
        ticket.boardName,
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();
      return haystack.includes(normalizedSearch);
    });
  }, [tickets, normalizedSearch]);

  const handleApprove = async (ticket: Ticket) => {
    if (!canReview) {
      return;
    }
    setPendingId(ticket.id);
    try {
      await onApprove(ticket.id);
    } finally {
      setPendingId(null);
    }
  };

  const submitReason = async () => {
    if (!reasonModal) {
      return;
    }
    const trimmed = reasonDraft.trim();
    if (!trimmed) {
      toast.error("A reason is required.");
      return;
    }
    if (trimmed.length > MAX_REASON) {
      toast.error(`Reason must be ${MAX_REASON} characters or fewer.`);
      return;
    }

    setPendingId(reasonModal.ticketId);
    try {
      const result =
        reasonModal.mode === "return"
          ? await onReturnForDetail(reasonModal.ticketId, trimmed)
          : await onReject(reasonModal.ticketId, trimmed);
      if (result) {
        setReasonModal(null);
        setReasonDraft("");
      }
    } finally {
      setPendingId(null);
    }
  };

  if (!isAuthenticated || !bootstrapComplete || needsConsent) {
    return null;
  }

  return (
    <div className="space-y-6">
      <div className="mb-6 rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
          <div>
            <h3 className="text-sm font-semibold text-gray-900 dark:text-slate-100">
              Approval Queue
            </h3>
            <p className="text-sm text-gray-500 dark:text-slate-400">
              Review submitted tickets before they enter active boards. Matches
              the Tickets workspace layout.
            </p>
          </div>
          <div className="flex flex-col items-stretch gap-2 sm:flex-row sm:items-center sm:justify-end sm:gap-3">
            {!loading && lastSuccessfulRefreshAt !== null ? (
              <div
                className="min-h-[1.25rem] text-right text-xs text-gray-500 dark:text-slate-400"
                aria-live="polite"
              >
                {silentRefreshing ? (
                  <span className="inline-flex items-center justify-end gap-2">
                    <span
                      className="inline-block h-3.5 w-3.5 shrink-0 animate-spin rounded-full border-2 border-gray-200 border-t-cortex-blue dark:border-slate-600 dark:border-t-cortex-cyan"
                      aria-hidden
                    />
                    <span>Updating…</span>
                  </span>
                ) : (
                  <span>{lastUpdatedDisplay}</span>
                )}
              </div>
            ) : null}
            <button
              type="button"
              onClick={() => void onRefresh()}
              className="inline-flex items-center justify-center rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 shadow-sm transition-colors hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            >
              Refresh
            </button>
          </div>
        </div>

        <div className="mt-4">
          <label className="block text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
            Search
          </label>
          <input
            type="search"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search title, ID, requester…"
            className="mt-1 w-full max-w-xl rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
            style={{ colorScheme: theme === "dark" ? "dark" : "light" }}
          />
        </div>
      </div>

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800 dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-200">
          {error}
        </div>
      )}

      {loading ? (
        <TicketGridSkeleton />
      ) : filteredTickets.length === 0 ? (
        <div className="rounded-xl border border-dashed border-gray-200 bg-gray-50/80 px-6 py-16 text-center dark:border-slate-800 dark:bg-slate-900/40">
          <p className="text-lg font-semibold text-gray-800 dark:text-slate-100">
            No tickets awaiting approval
          </p>
          <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
            New submissions appear here until a reviewer approves them.
          </p>
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {filteredTickets.map((ticket) => (
            <TicketCard
              key={ticket.id}
              ticket={ticket}
              onClick={() => void openTicketById(ticket.id)}
              approvalDisplayContext="approvalQueue"
              footerSlot={
                canReview ? (
                  <div className="flex flex-wrap gap-2">
                    <button
                      type="button"
                      disabled={pendingId === ticket.id}
                      onClick={() => void handleApprove(ticket)}
                      className="rounded-md bg-cortex-blue px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-50"
                    >
                      Approve
                    </button>
                    <button
                      type="button"
                      disabled={pendingId === ticket.id}
                      onClick={() => {
                        setReasonModal({ ticketId: ticket.id, mode: "return" });
                        setReasonDraft("");
                      }}
                      className="rounded-md border border-amber-300 bg-amber-50 px-3 py-1.5 text-xs font-semibold text-amber-950 transition-colors hover:bg-amber-100 disabled:opacity-50 dark:border-amber-700 dark:bg-amber-950/50 dark:text-amber-100 dark:hover:bg-amber-900/60"
                    >
                      Return for Detail
                    </button>
                    <button
                      type="button"
                      disabled={pendingId === ticket.id}
                      onClick={() => {
                        setReasonModal({ ticketId: ticket.id, mode: "reject" });
                        setReasonDraft("");
                      }}
                      className="rounded-md border border-red-300 bg-red-50 px-3 py-1.5 text-xs font-semibold text-red-800 transition-colors hover:bg-red-100 disabled:opacity-50 dark:border-red-800 dark:bg-red-950/40 dark:text-red-200 dark:hover:bg-red-950/60"
                    >
                      Reject
                    </button>
                  </div>
                ) : null
              }
            />
          ))}
        </div>
      )}

      {reasonModal ? (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 px-4">
          <div
            className="w-full max-w-lg rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-slate-800 dark:bg-slate-900"
            role="dialog"
            aria-modal="true"
            aria-labelledby="approval-reason-title"
          >
            <h2
              id="approval-reason-title"
              className="text-lg font-semibold text-gray-900 dark:text-slate-100"
            >
              {reasonModal.mode === "return"
                ? "Return for detail"
                : "Reject ticket"}
            </h2>
            <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
              {reasonModal.mode === "return"
                ? "Explain what the requester should clarify or add."
                : "Provide a concise reason for rejection."}
            </p>
            <textarea
              value={reasonDraft}
              onChange={(e) => setReasonDraft(e.target.value)}
              rows={5}
              className="mt-4 w-full rounded-md border border-gray-300 bg-white p-3 text-sm text-gray-900 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
              placeholder="Reason…"
            />
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => {
                  setReasonModal(null);
                  setReasonDraft("");
                }}
                className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-700 dark:border-slate-600 dark:text-slate-200"
              >
                Cancel
              </button>
              <button
                type="button"
                disabled={pendingId !== null}
                onClick={() => void submitReason()}
                className="rounded-md bg-cortex-blue px-4 py-2 text-sm font-semibold text-white hover:bg-cortex-blue-dark disabled:opacity-50"
              >
                Submit
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
