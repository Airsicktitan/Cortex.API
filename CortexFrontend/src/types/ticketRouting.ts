export interface TicketRoutingRule {
  id: number;
  boardId: string;
  priority: string;
  requesterDepartment: string;
  requesterRole: string;
  rulePriority: number;
  weight: number;
  department: string;
  titleContains: string;
  synitiOwner: string;
  businessOwner: string;
  isEnabled: boolean;
  /** True when the rule's owners resolve to currently eligible users. Defaults true for new drafts. */
  isValidConfiguration?: boolean;
  /** Reason the rule is invalid (e.g. "Syniti owner is not eligible"). Null/undefined when valid. */
  invalidReason?: string | null;
  createdDateUtc: string;
  lastModifiedDateUtc?: string;
}

export interface UpsertTicketRoutingRuleInput {
  boardId?: string;
  priority?: string;
  requesterDepartment?: string;
  requesterRole?: string;
  rulePriority: number;
  weight: number;
  department?: string;
  titleContains?: string;
  synitiOwner?: string;
  businessOwner?: string;
  isEnabled: boolean;
}
