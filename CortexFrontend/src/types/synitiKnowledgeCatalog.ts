/** GET /api/reference-catalogs/syniti-knowledge — read-only catalog visibility. */

export interface SynitiKnowledgeCatalogEntry {
  term: string;
  category: string;
  aliases: string | null;
  examplePhrases: string | null;
  shortDefinition: string;
  businessMeaning: string | null;
  technicalMeaning: string | null;
  suggestedReviewerChecks: string[];
  missingContextQuestions: string[];
  relatedTerms: string | null;
  sourceIsEnabled: boolean;
  sourceName: string;
  sourceType: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface SynitiKnowledgeCatalogListResponse {
  entries: SynitiKnowledgeCatalogEntry[];
}
