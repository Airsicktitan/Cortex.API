import type { ArchivedTicket } from "../types/archivedTicket";

type ArchivedTicketModalProps = {
  ticket: ArchivedTicket | null;
  onClose: () => void;
};

function formatDateTime(value?: string) {
  return value ? new Date(value).toLocaleString() : "—";
}

function formatValue(value?: string) {
  return value && value.trim() ? value : "—";
}

function DetailItem({
  label,
  value,
}: {
  label: string;
  value: string | number | undefined;
}) {
  return (
    <div>
      <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
        {label}
      </p>
      <p className="mt-1 text-sm text-gray-900 dark:text-slate-100">{value ?? "—"}</p>
    </div>
  );
}

export default function ArchivedTicketModal({
  ticket,
  onClose,
}: ArchivedTicketModalProps) {
  if (!ticket) {
    return null;
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-4 py-8"
      onClick={onClose}
      role="presentation"
    >
      <div
        className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-xl border border-gray-200 bg-white shadow-xl dark:border-slate-800 dark:bg-slate-900"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-label={`Archived ticket ${ticket.id}`}
      >
        <div className="flex items-start justify-between gap-4 border-b border-gray-100 px-6 py-5 dark:border-slate-800">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
              Archived Ticket
            </p>
            <h3 className="mt-1 text-lg font-semibold text-gray-900 dark:text-slate-100">
              {ticket.id}
            </h3>
            <p className="mt-1 text-sm text-gray-600 dark:text-slate-300">
              {ticket.title}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          >
            Close
          </button>
        </div>

        <div className="space-y-6 px-6 py-5">
          <section>
            <h4 className="text-sm font-semibold text-gray-900 dark:text-slate-100">
              Description
            </h4>
            <p className="mt-2 whitespace-pre-wrap text-sm text-gray-700 dark:text-slate-300">
              {formatValue(ticket.description)}
            </p>
          </section>

          <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <DetailItem label="Status" value={formatValue(ticket.status)} />
            <DetailItem label="Priority" value={formatValue(ticket.priority)} />
            <DetailItem label="Board" value={formatValue(ticket.boardName)} />
            <DetailItem
              label="Story Points"
              value={
                ticket.storyPoints === undefined ? "—" : String(ticket.storyPoints)
              }
            />
            <DetailItem label="Syniti Owner" value={formatValue(ticket.synitiOwner)} />
            <DetailItem
              label="Business Owner"
              value={formatValue(ticket.businessOwner)}
            />
            <DetailItem
              label="Created"
              value={formatDateTime(ticket.createdDate)}
            />
            <DetailItem
              label="Created By"
              value={formatValue(ticket.createdByDisplayName)}
            />
            <DetailItem
              label="Last Modified"
              value={formatDateTime(ticket.lastModifiedDate)}
            />
            <DetailItem
              label="Archived"
              value={formatDateTime(ticket.archivedDate)}
            />
            <DetailItem
              label="Archived By"
              value={formatValue(ticket.archivedByDisplayName)}
            />
            <DetailItem label="Comments" value={ticket.commentCount} />
            <DetailItem label="Attachments" value={ticket.attachmentCount} />
          </section>
        </div>
      </div>
    </div>
  );
}
