/** GET /api/tickets/{id}/syniti-knowledge-context */
export interface SynitiKnowledgeContextMatch {
  term: string;
  category: string;
  shortDefinition: string;
  businessMeaning?: string | null;
  technicalMeaning?: string | null;
  relatedTermsPreview?: string | null;
  sourceReason: string;
  matchStrengthLabel: string;
}

export interface SynitiKnowledgeContext {
  ticketId: string;
  matches: SynitiKnowledgeContextMatch[];
}
