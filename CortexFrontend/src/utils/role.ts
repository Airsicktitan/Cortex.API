/**
 * Auth0 role names — must match backend / Auth0 (case-sensitive canonical form).
 */
export const AUTH0_ROLES = {
  Admin: "Admin",
  Developer: "Developer",
  BusinessManager: "Business Manager",
  User: "User",
  Guest: "Guest",
} as const;

export type Auth0RoleName = (typeof AUTH0_ROLES)[keyof typeof AUTH0_ROLES];

const ALL_CANONICAL: readonly string[] = [
  AUTH0_ROLES.Admin,
  AUTH0_ROLES.Developer,
  AUTH0_ROLES.BusinessManager,
  AUTH0_ROLES.User,
  AUTH0_ROLES.Guest,
];

function normalizeOneRole(raw: string): string | null {
  const t = raw.trim();
  if (!t) {
    return null;
  }

  for (const c of ALL_CANONICAL) {
    if (c.toLowerCase() === t.toLowerCase()) {
      return c;
    }
  }

  return null;
}

/** Normalize role strings from the API or JWT (case-insensitive → canonical). */
export function normalizeRoles(
  roles: string[] | undefined | null,
  fallbackSingleRole?: string | null,
): string[] {
  const raw =
    roles && roles.length > 0
      ? roles
      : fallbackSingleRole
        ? [fallbackSingleRole]
        : [];
  const out: string[] = [];
  const seen = new Set<string>();
  for (const r of raw) {
    const c = normalizeOneRole(r);
    if (c && !seen.has(c)) {
      seen.add(c);
      out.push(c);
    }
  }
  return out;
}

export function hasRole(
  roles: string[] | undefined,
  role: string,
): boolean {
  const n = normalizeRoles(roles);
  const target = normalizeOneRole(role);
  if (!target) {
    return false;
  }

  return n.includes(target);
}

export function hasAnyRole(
  roles: string[] | undefined,
  ...check: string[]
): boolean {
  const n = normalizeRoles(roles);
  return check.some((c) => hasRole(n, c));
}

export function isAdmin(roles: string[] | undefined): boolean {
  return hasRole(roles, AUTH0_ROLES.Admin);
}

export function isDeveloper(roles: string[] | undefined): boolean {
  return hasRole(roles, AUTH0_ROLES.Developer);
}

export function isBusinessManager(roles: string[] | undefined): boolean {
  return hasRole(roles, AUTH0_ROLES.BusinessManager);
}

export function isStandardUser(roles: string[] | undefined): boolean {
  return hasRole(roles, AUTH0_ROLES.User);
}

export function isGuest(roles: string[] | undefined): boolean {
  return hasRole(roles, AUTH0_ROLES.Guest);
}

/** Admin or Developer — technical / elevated app access. */
export function hasElevatedAccess(roles: string[] | undefined): boolean {
  return hasAnyRole(roles, AUTH0_ROLES.Admin, AUTH0_ROLES.Developer);
}

/** Admin, Developer, or Business Manager — reports & business operations. */
export function hasBusinessAccess(roles: string[] | undefined): boolean {
  return hasAnyRole(
    roles,
    AUTH0_ROLES.Admin,
    AUTH0_ROLES.Developer,
    AUTH0_ROLES.BusinessManager,
  );
}

/** User+ excluding Guest — create tickets, comments, attachments. */
export function canCreateTickets(roles: string[] | undefined): boolean {
  return hasAnyRole(
    roles,
    AUTH0_ROLES.Admin,
    AUTH0_ROLES.Developer,
    AUTH0_ROLES.BusinessManager,
    AUTH0_ROLES.User,
  );
}

/** Business Manager+ — edit/delete tickets, archive, reactivate. */
export function canEditTickets(roles: string[] | undefined): boolean {
  return hasBusinessAccess(roles);
}

export function canManageUsers(roles: string[] | undefined): boolean {
  return hasElevatedAccess(roles);
}

export function canAccessConfig(roles: string[] | undefined): boolean {
  return hasElevatedAccess(roles);
}

export function canViewReports(roles: string[] | undefined): boolean {
  return hasBusinessAccess(roles);
}

export function canManageJobs(roles: string[] | undefined): boolean {
  return hasElevatedAccess(roles);
}

export function canViewJobActivity(roles: string[] | undefined): boolean {
  return hasBusinessAccess(roles);
}

/** Manage custom report *definitions* (settings API) — Developer+. */
export function canManageReportDefinitions(roles: string[] | undefined): boolean {
  return hasElevatedAccess(roles);
}
