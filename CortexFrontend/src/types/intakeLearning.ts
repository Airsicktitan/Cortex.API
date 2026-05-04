/** GET /api/reports/intake-learning — correlated follow-up friction; not causal proof. */

export interface IntakeLearningOverview {
  boardReturns: IntakeLearningGroup[];
  priorityReturns: IntakeLearningGroup[];
  departmentReturns: IntakeLearningGroup[];
  unknownDepartmentTicketCount: number;
  returnReasonAvailability: ReturnReasonAvailability;
  missingHintSummary: MissingHintSummary;
  generatedAtUtc: string;
  limitations: string[];
}

export interface IntakeLearningGroup {
  key: string;
  label: string;
  totalTickets: number;
  returnedTickets: number;
  returnRatePercent: number;
}

export interface ReturnReasonAvailability {
  returnedTickets: number;
  returnReasonStillAvailableCount: number;
  returnReasonAvailabilityPercent: number;
}

export interface MissingHintSummary {
  returnedTickets: number;
  returnedTicketsWithMissingHintJson: number;
  averageMissingHintCount: number;
  zeroHintsCount: number;
  oneToTwoHintsCount: number;
  threeToFiveHintsCount: number;
  sixPlusHintsCount: number;
}
