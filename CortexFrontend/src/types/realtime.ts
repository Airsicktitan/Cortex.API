export interface RealtimeEvent {
  eventType: string;
  ticketId?: string;
  entityId?: string;
  occurredDateUtc: string;
}
