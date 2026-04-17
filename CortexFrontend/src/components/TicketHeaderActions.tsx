import { memo } from "react";

type TicketHeaderActionsProps = {
  canCreateTickets: boolean;
  onRefresh: () => void;
  onCreateTicket: () => void;
};

function TicketHeaderActions({
  canCreateTickets,
  onRefresh,
  onCreateTicket,
}: TicketHeaderActionsProps) {
  return (
    <>
      <button
        onClick={onRefresh}
        className="inline-flex items-center rounded-md bg-cortex-blue px-3 py-2 text-sm font-medium text-white shadow-sm transition-colors hover:bg-cortex-blue-dark"
      >
        Refresh
      </button>

      {canCreateTickets && (
        <button
          onClick={onCreateTicket}
          className="inline-flex items-center rounded-md bg-cortex-cyan px-3.5 py-2 text-sm font-semibold text-cortex-ink shadow-sm ring-1 ring-cortex-cyan/70 transition-colors hover:bg-cortex-blue hover:text-white dark:bg-cortex-cyan dark:text-cortex-ink dark:ring-cortex-cyan/60 dark:hover:bg-cortex-blue dark:hover:text-white"
        >
          + New Ticket
        </button>
      )}
    </>
  );
}

export default memo(TicketHeaderActions);
