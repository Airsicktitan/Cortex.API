import type { Ticket } from "../types/ticket";

const CLOSED_STATUSES = new Set(["resolved", "closed"]);

function normalizeStatus(value: string | undefined) {
  return value?.trim().toLowerCase() ?? "";
}

/** Open / in-flight ticket (not resolved or closed). */
export function isClosedTicket(ticket: Ticket): boolean {
  return CLOSED_STATUSES.has(normalizeStatus(ticket.status));
}

export function isOpenTicket(ticket: Ticket): boolean {
  return !isClosedTicket(ticket);
}
