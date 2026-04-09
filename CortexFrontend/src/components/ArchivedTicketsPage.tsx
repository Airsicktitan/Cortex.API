import type { ArchivedTicket } from "../types/archivedTicket";
import { ArchivedTicketsSkeleton } from "./LoadingSkeletons";

interface ArchivedTicketsPageProps {
  tickets: ArchivedTicket[];
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
  canReactivate: boolean;
  reactivatingTicketId: string | null;
  onReactivate: (ticket: ArchivedTicket) => Promise<void>;
}

function formatDateTime(value?: string) {
  return value ? new Date(value).toLocaleString() : "—";
}

function formatValue(value?: string) {
  return value && value.trim() ? value : "—";
}

export default function ArchivedTicketsPage({
  tickets,
  loading,
  error,
  onRefresh,
  canReactivate,
  reactivatingTicketId,
  onReactivate,
}: ArchivedTicketsPageProps) {
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
                Reactivating restores the ticket to the active queue. Archived
                comment and attachment counts are preserved as history only.
              </p>
            )}
          </div>

          <div className="flex items-center gap-3">
            <span className="text-sm text-gray-500 dark:text-slate-400">
              {tickets.length} archived ticket{tickets.length === 1 ? "" : "s"}
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
        {tickets.length === 0 ? (
          <div className="px-6 py-12 text-center text-gray-500 dark:text-slate-400">
            No archived tickets found.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                <tr>
                  <th className="px-4 py-3 font-medium">Ticket</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">Priority</th>
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
                    className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                  >
                    <td className="px-4 py-3 align-top">
                      <div>
                        <p className="font-medium text-gray-900 dark:text-slate-100">
                          {ticket.id}
                        </p>
                        <p className="max-w-xs truncate text-gray-500 dark:text-slate-400">
                          {ticket.title}
                        </p>
                      </div>
                    </td>
                    <td className="px-4 py-3 align-top">{formatValue(ticket.status)}</td>
                    <td className="px-4 py-3 align-top">{formatValue(ticket.priority)}</td>
                    <td className="px-4 py-3 align-top">
                      {formatValue(ticket.synitiOwner)}
                    </td>
                    <td className="px-4 py-3 align-top">
                      {formatValue(ticket.businessOwner)}
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      <div>
                        <p>{formatDateTime(ticket.createdDate)}</p>
                        <p className="text-xs text-gray-500 dark:text-slate-400">
                          {ticket.createdByDisplayName}
                        </p>
                      </div>
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDateTime(ticket.archivedDate)}
                    </td>
                    <td className="px-4 py-3 align-top">
                      {formatValue(ticket.archivedByDisplayName)}
                    </td>
                    <td className="px-4 py-3 align-top">{ticket.commentCount}</td>
                    <td className="px-4 py-3 align-top">{ticket.attachmentCount}</td>
                    {canReactivate && (
                      <td className="px-4 py-3 align-top">
                        <button
                          onClick={() => void onReactivate(ticket)}
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
          </div>
        )}
      </section>
    </div>
  );
}
