export interface TicketBoardDefinition {
  id: number;
  name: string;
  description?: string;
  requiresStoryPoints: boolean;
  isEnabled: boolean;
  createdDateUtc: string;
  lastModifiedDateUtc?: string;
}

export interface UpsertTicketBoardDefinitionInput {
  name?: string;
  description?: string;
  requiresStoryPoints: boolean;
  isEnabled: boolean;
}
