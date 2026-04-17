import type { ArchivedTicket } from "./archivedTicket";
import type { Ticket } from "./ticket";

export type PagedTicketList = {
  items: Ticket[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type PagedArchivedTicketList = {
  items: ArchivedTicket[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};
