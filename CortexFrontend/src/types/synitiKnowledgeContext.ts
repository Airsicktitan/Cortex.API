/** GET /api/tickets/{id}/syniti-knowledge-context */
export interface SynitiKnowledgeContextMatch {
  term: string;
  category: string;
  shortDefinition: string;
  /** Curated reviewer-first guidance from the safe catalog. */
  reviewerGuidance: string;
  businessMeaning?: string | null;
  technicalMeaning?: string | null;
  relatedTermsPreview?: string | null;
  sourceReason: string;
  matchStrengthLabel: string;
  suggestedReviewerChecks?: string[];
  missingContextQuestions?: string[];
}

export interface SynitiKnowledgeContext {
  ticketId: string;
  matches: SynitiKnowledgeContextMatch[];
}
