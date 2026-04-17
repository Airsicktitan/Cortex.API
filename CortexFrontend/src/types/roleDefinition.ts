export interface RoleDefinition {
  id: number;
  name: string;
  description?: string;
  permissions: string[];
  isEnabled: boolean;
  createdDateUtc: string;
  lastModifiedDateUtc?: string;
}

export interface UpsertRoleDefinitionInput {
  name: string;
  description?: string;
  permissions: string[];
  isEnabled: boolean;
}
