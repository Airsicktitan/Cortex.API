export interface RealtimeEvent {
  eventType: string;
  ticketId?: string;
  entityId?: string;
  recipientUserIds?: number[];
  occurredDateUtc: string;
}
