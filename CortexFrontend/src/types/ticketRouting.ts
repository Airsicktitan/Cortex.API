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
