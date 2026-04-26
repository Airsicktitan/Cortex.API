export interface CortexInsightSimilarTicket {
  id: string;
  sourceTicketId: string;
  sourceUrl: string;
  title: string;
  description: string;
  status: string;
  lastMeaningfulComment?: string | null;
  sourceQuote?: string | null;
  createdDate: string;
  lastModifiedDate?: string | null;
  similarityScore: number;
  confidenceScore: number;
  matchReasons: string[];
}

export interface CortexInsight {
  ticketId: string;
  matches: CortexInsightSimilarTicket[];
  confidenceScore: number;
  matchReasons: string[];
  summary?: string | null;
  resolution?: string | null;
  rootCause?: string | null;
  suggestedNextStep?: string | null;
  unavailable: boolean;
  unavailableReason?: string | null;
}
