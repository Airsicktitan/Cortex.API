export interface Ticket {
  id: string;
  title: string;
  description: string;
  status: string;
  priority: string;
  synitiOwner?: string;
  businessOwner?: string;
  createdBy: string;
  createdDate: string;
  lastModifiedBy?: string;
  lastModifiedDate?: string;
}
