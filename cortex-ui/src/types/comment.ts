export interface Comment {
  id: number;
  ticketId: string;
  body: string;
  createdBy: string;
  createdDate: string;
  lastModifiedDate: string;
  createdByUser?: {
    id: string;
    displayName: string;
  };
}
