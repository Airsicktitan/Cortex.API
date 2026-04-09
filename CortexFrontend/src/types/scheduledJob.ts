export type ScheduledJobType = "ArchiveEligibleTickets" | "RunStoredProcedure";

export interface ScheduledJob {
  id: number;
  name: string;
  description?: string;
  jobType: ScheduledJobType;
  intervalMinutes: number;
  isEnabled: boolean;
  storedProcedureDefinitionId?: number;
  storedProcedureName?: string;
  runAsUserId: number;
  runAsDisplayName: string;
  createdDateUtc: string;
  lastModifiedDateUtc?: string;
  lastRunDateUtc?: string;
  nextRunDateUtc?: string;
  lastRunStatus?: string;
  lastRunMessage?: string;
}

export interface UpsertScheduledJobInput {
  name: string;
  description?: string;
  jobType: ScheduledJobType;
  intervalMinutes: number;
  isEnabled: boolean;
  storedProcedureDefinitionId?: number;
}
