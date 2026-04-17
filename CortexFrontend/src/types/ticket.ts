/** Result of saving from the ticket modal (parent may keep the modal open after a conflict reload). */
export type TicketSaveOutcome = "saved" | "reloaded";

export interface Ticket {
  id: string;
  title: string;
  description: string;
  status: string;
  priority: string;
  department?: string;
  boardId: number;
  boardName: string;
  storyPoints?: number;
  synitiOwner?: string;
  businessOwner?: string;
  createdBy: string;
  createdByUser?:
    | {
        id: number;
        displayName: string;
      }
    | undefined;
  createdDate: string;
  lastModifiedBy?: string;
  lastModifiedDate?: string;
  createdByDisplayName?: string;
  slaTargetDate: string;
  slaCompletedDate?: string;
  slaStatus: string;
  slaRemainingMinutes: number;
  isSlaBreached: boolean;
  /** Base64 row version from API; required when updating an existing ticket. */
  concurrencyToken?: string;
}

export interface TicketMutationInput {
  title?: string;
  description?: string;
  status?: string;
  priority?: string;
  department?: string;
  boardId?: number;
  storyPoints?: number;
  synitiOwner?: string;
  businessOwner?: string;
  changeReason?: string;
  concurrencyToken?: string;
}

export interface CreateTicketInput extends Omit<TicketMutationInput, "status"> {
  title: string;
  description: string;
  priority: string;
}
