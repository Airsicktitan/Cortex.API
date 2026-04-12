export interface TicketRoutingRule {
  id: number;
  department: string;
  titleContains: string;
  synitiOwner: string;
  businessOwner: string;
  isEnabled: boolean;
  createdDateUtc: string;
  lastModifiedDateUtc?: string;
}

export interface UpsertTicketRoutingRuleInput {
  department?: string;
  titleContains?: string;
  synitiOwner?: string;
  businessOwner?: string;
  isEnabled: boolean;
}
