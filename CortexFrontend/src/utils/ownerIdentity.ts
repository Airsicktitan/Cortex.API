import type { UserDirectoryEntry } from "../types/user";

/** Matches backend `OwnerFieldResolution.UserIdTokenPrefix`. */
export const USER_ID_TOKEN_PREFIX = "user:";

export function normalizeOwnerToken(value: string | undefined): string {
  return value?.trim().toLowerCase() ?? "";
}

function meaningfulLabel(value: string | undefined): string | null {
  const s = value?.trim();
  if (!s) {
    return null;
  }
  const lower = s.toLowerCase();
  if (s === "-" || lower === "n/a" || lower === "none") {
    return null;
  }
  return s;
}

/** Emails that are not useful in the owner dropdown (Auth0 plumbing, placeholders). */
export function isTechnicalOrSyntheticEmail(email: string | undefined): boolean {
  const e = normalizeOwnerToken(email);
  if (!e) {
    return true;
  }
  if (e.endsWith("@unknown.local") || e.endsWith("@localhost")) {
    return true;
  }
  const at = e.indexOf("@");
  if (at <= 0) {
    return true;
  }
  const local = e.slice(0, at);
  if (local.startsWith("auth0-") || local.startsWith("auth0|")) {
    return true;
  }
  return false;
}

export function emailUsableAsSubtitle(email: string | undefined): boolean {
  if (isTechnicalOrSyntheticEmail(email)) {
    return false;
  }
  const t = email?.trim();
  return Boolean(t && t.includes("@"));
}

/** Stable value persisted on tickets. Display names/emails are presentation only. */
export function ownerStorageValue(entry: UserDirectoryEntry): string {
  return `${USER_ID_TOKEN_PREFIX}${entry.id}`;
}

/**
 * Human-readable label for a stored owner field (email, `user:id`, or legacy display name).
 * When `users` is empty, still formats `user:` tokens and passes through emails / legacy text.
 */
export function ownerDisplayLabel(
  stored: string | undefined,
  users: UserDirectoryEntry[],
): string {
  const t = stored?.trim() ?? "";
  if (!t) {
    return "";
  }
  const norm = normalizeOwnerToken(t);
  if (norm.startsWith(USER_ID_TOKEN_PREFIX)) {
    const idStr = norm.slice(USER_ID_TOKEN_PREFIX.length);
    const id = Number.parseInt(idStr, 10);
    if (!Number.isNaN(id)) {
      const u = users.find((x) => x.id === id);
      if (u) {
        return u.displayName;
      }
      return "Unknown user";
    }
  }

  const byEmail = users.find((u) => normalizeOwnerToken(u.email) === norm);
  if (byEmail) {
    return byEmail.displayName;
  }

  const byDisplay = users.find((u) => normalizeOwnerToken(u.displayName) === norm);
  if (byDisplay) {
    return byDisplay.displayName;
  }

  return t;
}

/** Display helper when no directory is available (list cards, exports). */
export function formatOwnerFieldForDisplay(stored?: string | null): string {
  return ownerDisplayLabel(stored ?? "", []);
}

/** Read-only label: prefer API-resolved display name from the ticket payload. */
export function readOnlySynitiOwnerLabel(ticket: {
  synitiOwner?: string;
  synitiOwnerDisplayName?: string;
}): string {
  const d = ticket.synitiOwnerDisplayName?.trim();
  if (d) {
    return d;
  }
  return formatOwnerFieldForDisplay(ticket.synitiOwner);
}

/** Read-only label: prefer API-resolved display name from the ticket payload. */
export function readOnlyBusinessOwnerLabel(ticket: {
  businessOwner?: string;
  businessOwnerDisplayName?: string;
}): string {
  const d = ticket.businessOwnerDisplayName?.trim();
  if (d) {
    return d;
  }
  return formatOwnerFieldForDisplay(ticket.businessOwner);
}

/**
 * Read-only owner line in ticket modal: when the field matches the saved ticket, use API display
 * names; when the user has edited the owner, resolve from the user directory.
 */
export function readOnlyOwnerDetailDisplay(
  stored: string,
  options: {
    baselineStored: string;
    apiDisplayName?: string;
    directory: UserDirectoryEntry[];
  },
): string {
  const dirty =
    (stored ?? "").trim() !== (options.baselineStored ?? "").trim();
  if (dirty && options.directory.length > 0) {
    const fromDir = ownerDisplayLabel(stored, options.directory);
    if (fromDir) {
      return fromDir;
    }
  }
  if (!dirty && options.apiDisplayName?.trim()) {
    return options.apiDisplayName.trim();
  }
  return formatOwnerFieldForDisplay(stored);
}

export function storedOwnerMatchesUser(
  stored: string | undefined,
  user: UserDirectoryEntry,
): boolean {
  const norm = normalizeOwnerToken(stored);
  if (!norm) {
    return false;
  }
  if (norm === normalizeOwnerToken(ownerStorageValue(user))) {
    return true;
  }
  if (norm === normalizeOwnerToken(user.displayName)) {
    return true;
  }
  if (!isTechnicalOrSyntheticEmail(user.email) && norm === normalizeOwnerToken(user.email)) {
    return true;
  }
  return false;
}

export function computeDuplicateDisplayNames(users: UserDirectoryEntry[]): Set<string> {
  const counts = new Map<string, number>();
  for (const u of users) {
    const k = normalizeOwnerToken(u.displayName);
    counts.set(k, (counts.get(k) ?? 0) + 1);
  }
  const dup = new Set<string>();
  for (const [k, v] of counts) {
    if (v > 1) {
      dup.add(k);
    }
  }
  return dup;
}

function deptRoleLine(user: UserDirectoryEntry): string | null {
  const dept = meaningfulLabel(user.department);
  const role = meaningfulLabel(user.role);
  if (dept && role) {
    return `${dept} · ${role}`;
  }
  if (dept) {
    return dept;
  }
  if (role) {
    return role;
  }
  return null;
}

const deptRoleKey = (user: UserDirectoryEntry) => normalizeOwnerToken(deptRoleLine(user) ?? "");

/**
 * Subtitle lines for owner dropdown options. Duplicate display names get dept/role; if still
 * ambiguous, a real email line is added (never synthetic Auth0-style addresses).
 * Pass {@link duplicateDisplayNames} from a single memoized `computeDuplicateDisplayNames(users)` per list.
 */
export function getOwnerOptionSubtitleLines(
  user: UserDirectoryEntry,
  users: UserDirectoryEntry[],
  duplicateDisplayNames: Set<string>,
): { line2: string | null; line3: string | null } {
  const isDup = duplicateDisplayNames.has(normalizeOwnerToken(user.displayName));

  if (!isDup) {
    const dept = meaningfulLabel(user.department);
    const role = meaningfulLabel(user.role);
    if (dept && role) {
      return { line2: `${dept} · ${role}`, line3: null };
    }
    if (dept) {
      return { line2: dept, line3: null };
    }
    if (role) {
      return { line2: role, line3: null };
    }
    if (emailUsableAsSubtitle(user.email)) {
      return { line2: user.email!.trim(), line3: null };
    }
    return { line2: null, line3: null };
  }

  const line2 = deptRoleLine(user);
  const group = users.filter(
    (u) => normalizeOwnerToken(u.displayName) === normalizeOwnerToken(user.displayName),
  );
  const ambiguous = group.filter((u) => deptRoleKey(u) === deptRoleKey(user)).length > 1;
  const line3 =
    ambiguous && emailUsableAsSubtitle(user.email) ? user.email!.trim() : null;
  return { line2, line3 };
}
