export interface TicketStatusDefinition {
  id: number;
  name: string;
  description?: string;
  isEnabled: boolean;
  createdDateUtc: string;
  lastModifiedDateUtc?: string;
}

export interface UpsertTicketStatusDefinitionInput {
  name: string;
  description?: string;
  isEnabled: boolean;
}
