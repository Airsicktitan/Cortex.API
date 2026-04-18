export interface ArchivedTicket {
  id: string;
  title: string;
  description: string;
  status: string;
  priority: string;
  boardId: number;
  boardName: string;
  storyPoints?: number;
  synitiOwner?: string;
  businessOwner?: string;
  synitiOwnerDisplayName?: string;
  businessOwnerDisplayName?: string;
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
