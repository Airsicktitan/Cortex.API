export interface TicketAttachment {
  id: number;
  ticketId: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  uploadedBy: number;
  uploadedByDisplayName: string;
  uploadedDate: string;
}
