export interface UserProfile {
  id: number;
  displayName: string;
  nickName?: string;
  email: string;
  phoneNumber?: string;
  department?: string;
  role: string;
  createdDate: string;
  isActive: boolean;
  lastLoginDate?: string;
  expiryDate?: string;
  lastModifiedDate?: string;
}

export interface UserRecord extends UserProfile {
  auth0Id?: string;
}

export interface AdminUpdateUserInput {
  nickName?: string;
  phoneNumber?: string;
  department?: string;
  role?: string;
  isActive?: boolean;
  expiryDate?: string | null;
}

export interface UpdateUserProfileInput {
  displayName?: string;
  nickName?: string;
  phoneNumber?: string;
  department?: string;
}
