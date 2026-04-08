export interface ArchivedTicket {
  id: string;
  title: string;
  description: string;
  status: string;
  priority: string;
  synitiOwner?: string;
  businessOwner?: string;
  createdBy: number;
  createdByDisplayName: string;
  createdDate: string;
  lastModifiedBy: number;
  lastModifiedDate?: string;
  archivedBy: number;
  archivedByDisplayName: string;
  archivedDate: string;
  commentCount: number;
  attachmentCount: number;
}
