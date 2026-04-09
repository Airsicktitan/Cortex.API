export interface TicketAuditFieldChange {
  fieldName: string;
  oldValue?: string | null;
  newValue?: string | null;
}

export interface TicketAuditEntry {
  id: number;
  ticketId: string;
  action: string;
  summary: string;
  reason?: string | null;
  changedBy: number;
  changedByDisplayName: string;
  changedDateUtc: string;
  fieldChanges: TicketAuditFieldChange[];
}
