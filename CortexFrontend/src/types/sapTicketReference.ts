/** GET /api/tickets/{id}/sap-reference-context */
export type SapTicketReferenceMatchType = "Table" | "Field" | "DomainValue";

export type SapTicketReferenceMatchConfidence = "High" | "Medium" | "Low";

export interface SapTicketReferenceMatch {
  matchType: SapTicketReferenceMatchType;
  matchedText: string;
  tableName: string | null;
  tableDescription: string | null;
  fieldName: string | null;
  fieldDescription: string | null;
  domainName: string | null;
  domainValue: string | null;
  sourceName: string;
  module: string | null;
  businessObject: string | null;
  dataDomain: string | null;
  isCustom: boolean;
  confidence: SapTicketReferenceMatchConfidence;
  reason: string;
  tableId?: number | null;
  fieldId?: number | null;
  sourceId?: number | null;
}

export interface SapTicketReferenceContext {
  ticketId: string;
  matches: SapTicketReferenceMatch[];
  /**
   * True when the ticket text suggests SAP work but no catalog table/field match was returned.
   * Intake/readiness only — not a metadata detection claim.
   */
  sapIntentOnly?: boolean;
}
