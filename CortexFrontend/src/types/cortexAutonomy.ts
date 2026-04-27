export type CortexAutonomyMode = "Disabled" | "Shadow" | "AutoApplied" | string;

/** Effective settings mode for the control panel: Disabled / Shadow / Active. */
export type CortexAutonomySettingsMode = "Disabled" | "Shadow" | "Active" | string;

export interface CortexAutonomySettings {
  enabled: boolean;
  shadowMode: boolean;
  minConfidence: number;
  recentOverrideWindowHours: number;
  requireClearWinner: boolean;
  minAlternativeGap: number;
  lastModifiedDateUtc?: string | null;
  lastModifiedByDisplayName?: string | null;
  mode: CortexAutonomySettingsMode;
}

export interface CortexAutonomyCounts {
  evaluated: number;
  eligible: number;
  autoApplied: number;
  blocked: number;
}

export interface CortexAutonomyRecentDecision {
  ticketId: string;
  ticketTitle?: string | null;
  recommendedOwnerId?: string | null;
  recommendedOwnerName?: string | null;
  mode: CortexAutonomyMode;
  isEligible: boolean;
  wasAutoApplied: boolean;
  confidence: number;
  result: "AutoApplied" | "Eligible" | "Blocked" | string;
  resultLabel: string;
  reasonSummary: string;
  evaluatedAtUtc: string;
}

export interface CortexAutonomySummary {
  settings: CortexAutonomySettings;
  counts: CortexAutonomyCounts;
  recent: CortexAutonomyRecentDecision[];
  windowStartUtc: string;
  windowEndUtc: string;
}

export interface UpdateCortexAutonomySettingsInput {
  enabled?: boolean;
  shadowMode?: boolean;
  minConfidence?: number;
  recentOverrideWindowHours?: number;
  requireClearWinner?: boolean;
  minAlternativeGap?: number;
}

export interface CortexAutonomyResult {
  ticketId: string;
  isEligible: boolean;
  wasAutoApplied: boolean;
  mode: CortexAutonomyMode;
  recommendedOwnerId?: string | null;
  recommendedOwnerName?: string | null;
  previousOwnerId?: string | null;
  confidence: number;
  learningAdjustment?: number | null;
  decisionVersion: string;
  passedChecks: string[];
  blockedReasons: string[];
  summary: string;
  evaluatedAtUtc?: string | null;
  appliedAtUtc?: string | null;
}
