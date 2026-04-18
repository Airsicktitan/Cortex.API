import { useCallback, useState, type UIEvent } from "react";
import type { ArchivedTicket } from "../types/archivedTicket";
import ArchivedTicketModal from "./ArchivedTicketModal";
import { ArchivedTicketsSkeleton } from "./LoadingSkeletons";
import {
  formatDisplayDateTime,
  formatDisplayValue,
  formatTicketIdentifier,
} from "../utils/presentation";
import {
  readOnlyBusinessOwnerLabel,
  readOnlySynitiOwnerLabel,
} from "../utils/ownerIdentity";

interface ArchivedTicketsPageProps {
  tickets: ArchivedTicket[];
  totalTickets: number;
  searchQuery: string;
  onSearchQueryChange: (value: string) => void;
  loading: boolean;
  error: string | null;
  highlightedTicketId?: string | null;
  onRefresh: () => void;
  onLoadMore: () => void;
  hasMore: boolean;
  loadingMore: boolean;
  canReactivate: boolean;
  reactivatingTicketId: string | null;
  onReactivate: (ticket: ArchivedTicket) => Promise<void>;
}

export default function ArchivedTicketsPage({
  tickets,
  totalTickets,
  searchQuery,
  onSearchQueryChange,
  loading,
  error,
  highlightedTicketId,
  onRefresh,
  onLoadMore,
  hasMore,
  loadingMore,
  canReactivate,
  reactivatingTicketId,
  onReactivate,
}: ArchivedTicketsPageProps) {
  const [selectedTicket, setSelectedTicket] = useState<ArchivedTicket | null>(
    null,
  );

  const handleContainerScroll = useCallback(
    (event: UIEvent<HTMLDivElement>) => {
      if (!hasMore || loadingMore || loading) {
        return;
      }

      const target = event.currentTarget;
      const remaining = target.scrollHeight - target.scrollTop - target.clientHeight;
      if (remaining < 220) {
        onLoadMore();
      }
    },
    [hasMore, loading, loadingMore, onLoadMore],
  );

  if (loading) {
    return <ArchivedTicketsSkeleton />;
  }

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
              Archived Tickets
            </h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">
              Tickets that were moved out of the active queue and preserved for history.
            </p>
            {canReactivate && (
              <p className="mt-2 text-xs text-amber-700 dark:text-amber-300">
                Reactivating restores the ticket to the active queue along with
                its archived comments and attachments. Older legacy archives may
                include recovered placeholder files when the original attachment
                binary was never stored.
              </p>
            )}
          </div>

          <div className="flex items-center gap-3">
            <span className="text-sm text-gray-500 dark:text-slate-400">
              {totalTickets} archived ticket{totalTickets === 1 ? "" : "s"}
            </span>
            <button
              onClick={onRefresh}
              className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            >
              Refresh
            </button>
          </div>
        </div>
      </section>

      {error && (
        <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      <section className="overflow-hidden rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-gray-100 px-4 py-3 dark:border-slate-800">
          <input
            type="text"
            value={searchQuery}
            onChange={(event) => onSearchQueryChange(event.target.value)}
            placeholder="Search archived tickets"
            className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
          />
        </div>
        {tickets.length === 0 ? (
          <div className="px-6 py-12 text-center text-gray-500 dark:text-slate-400">
            No archived tickets found.
          </div>
        ) : (
          <div
            className="scroll-surface max-h-[70vh] overflow-auto"
            onScroll={handleContainerScroll}
          >
            <table className="min-w-full text-sm">
              <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                <tr>
                  <th className="px-4 py-3 font-medium">Ticket</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">Priority</th>
                  <th className="px-4 py-3 font-medium">Board</th>
                  <th className="px-4 py-3 font-medium">Syniti Owner</th>
                  <th className="px-4 py-3 font-medium">Business Owner</th>
                  <th className="px-4 py-3 font-medium">Created</th>
                  <th className="px-4 py-3 font-medium">Archived</th>
                  <th className="px-4 py-3 font-medium">Archived By</th>
                  <th className="px-4 py-3 font-medium">Comments</th>
                  <th className="px-4 py-3 font-medium">Attachments</th>
                  {canReactivate && (
                    <th className="px-4 py-3 font-medium">Actions</th>
                  )}
                </tr>
              </thead>
              <tbody>
                {tickets.map((ticket) => (
                  <tr
                    key={ticket.id}
                    id={`archived-ticket-${ticket.id}`}
                    onClick={() => setSelectedTicket(ticket)}
                    className={`border-t text-gray-700 dark:text-slate-200 ${
                      highlightedTicketId === ticket.id
                        ? "border-cortex-cyan bg-cortex-blue-soft/50 dark:border-cortex-cyan dark:bg-cortex-blue/10"
                        : "border-gray-100 dark:border-slate-800"
                    } cursor-pointer transition-colors hover:bg-gray-50 dark:hover:bg-slate-800/60`}
                  >
                    <td className="px-4 py-3 align-top">
                      <div>
                        <p className="font-medium text-gray-900 dark:text-slate-100">
                          {formatTicketIdentifier(ticket.id)}
                        </p>
                        <p className="max-w-xs truncate text-gray-500 dark:text-slate-400">
                          {ticket.title}
                        </p>
                      </div>
                    </td>
                    <td className="px-4 py-3 align-top">{formatDisplayValue(ticket.status)}</td>
                    <td className="px-4 py-3 align-top">{formatDisplayValue(ticket.priority)}</td>
                    <td className="px-4 py-3 align-top">
                      <div>
                        <p>{formatDisplayValue(ticket.boardName)}</p>
                        {ticket.storyPoints !== undefined && ticket.storyPoints !== null ? (
                          <p className="text-xs text-gray-500 dark:text-slate-400">
                            {ticket.storyPoints} story point
                            {ticket.storyPoints === 1 ? "" : "s"}
                          </p>
                        ) : null}
                      </div>
                    </td>
                    <td className="px-4 py-3 align-top">
                      {formatDisplayValue(readOnlySynitiOwnerLabel(ticket))}
                    </td>
                    <td className="px-4 py-3 align-top">
                      {formatDisplayValue(readOnlyBusinessOwnerLabel(ticket))}
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      <div>
                        <p>{formatDisplayDateTime(ticket.createdDate)}</p>
                        <p className="text-xs text-gray-500 dark:text-slate-400">
                          {ticket.createdByDisplayName}
                        </p>
                      </div>
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDisplayDateTime(ticket.archivedDate)}
                    </td>
                    <td className="px-4 py-3 align-top">
                      {formatDisplayValue(ticket.archivedByDisplayName)}
                    </td>
                    <td className="px-4 py-3 align-top">{ticket.commentCount}</td>
                    <td className="px-4 py-3 align-top">{ticket.attachmentCount}</td>
                    {canReactivate && (
                      <td className="px-4 py-3 align-top">
                        <button
                          onClick={(event) => {
                            event.stopPropagation();
                            void onReactivate(ticket);
                          }}
                          disabled={reactivatingTicketId === ticket.id}
                          className="rounded-md bg-cortex-blue px-3 py-2 text-sm text-white transition-colors hover:bg-cortex-blue-dark disabled:cursor-not-allowed disabled:opacity-60"
                        >
                          {reactivatingTicketId === ticket.id
                            ? "Reactivating..."
                            : "Reactivate"}
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
            {loadingMore && (
              <div className="sticky bottom-0 border-t border-gray-200 bg-white/95 px-4 py-3 text-center text-sm text-gray-500 backdrop-blur dark:border-slate-800 dark:bg-slate-900/95 dark:text-slate-300">
                Loading more archived tickets...
              </div>
            )}
          </div>
        )}
      </section>

      <ArchivedTicketModal
        ticket={selectedTicket}
        onClose={() => setSelectedTicket(null)}
      />
    </div>
  );
}
