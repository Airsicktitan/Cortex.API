export interface Comment {
  id: number;
  ticketId: string;
  body: string;
  createdBy: number;
  createdByDisplayName?: string;
  createdDate: string;
  lastModifiedDate: string;
}