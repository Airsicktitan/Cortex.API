export interface Ticket {
  id: string;
  title: string;
  description: string;
  status: string;
  priority: string;
  department?: string;
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
}

export interface TicketMutationInput {
  title?: string;
  description?: string;
  status?: string;
  priority?: string;
  department?: string;
  synitiOwner?: string;
  businessOwner?: string;
  changeReason?: string;
}

export interface CreateTicketInput extends TicketMutationInput {
  title: string;
}
