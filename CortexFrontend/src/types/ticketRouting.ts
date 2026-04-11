export interface TicketRoutingRule {
  id: number;
  department: string;
  synitiOwner: string;
  isEnabled: boolean;
  createdDateUtc: string;
  lastModifiedDateUtc?: string;
}

export interface UpsertTicketRoutingRuleInput {
  department: string;
  synitiOwner: string;
  isEnabled: boolean;
}
