import type { ArchivedTicket } from "./archivedTicket";
import type { Comment } from "./comment";
import type { UserNotification } from "./notification";
import type { Ticket } from "./ticket";

export interface RealtimeEvent {
  eventType: string;
  ticketId?: string;
  entityId?: string;
  actorUserId?: number;
  actorDisplayName?: string;
  ticket?: Ticket;
  archivedTicket?: ArchivedTicket;
  comment?: Comment;
  notifications?: UserNotification[];
  unreadCount?: number;
  occurredDateUtc: string;
}
