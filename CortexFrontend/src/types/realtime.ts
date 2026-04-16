export interface RealtimeEvent {
  eventType: string;
  ticketId?: string;
  entityId?: string;
  actorUserId?: number;
  actorDisplayName?: string;
  recipientUserIds?: number[];
  occurredDateUtc: string;
}
