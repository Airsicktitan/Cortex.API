export interface Comment {
  id: number;
  ticketId: string;
  body: string;
  createdBy: number;
  createdDate: string;
  lastModifiedDate: string;
  createdByDisplayName?: string;
  createdByUser?: {
    id: string;
    displayName: string;
  };
}
