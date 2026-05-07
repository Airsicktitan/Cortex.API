import type { AdminUpdateUserInput, UpdateUserProfileInput } from "../types/user";

type PayloadWithOptionalNames = {
  displayName?: string;
  nickName?: string;
};

function trimOrDropKey<T extends PayloadWithOptionalNames>(
  payload: T,
  key: keyof PayloadWithOptionalNames,
): void {
  const raw = payload[key];
  if (raw === undefined) {
    return;
  }
  const t = raw.trim();
  if (t.length === 0) {
    delete payload[key];
  } else {
    (payload as Record<string, string>)[key as string] = t;
  }
}

/**
 * Nickname supports intentional clearing: send an empty string so the API applies field-present
 * semantics (omit property = unchanged). Whitespace-only becomes an empty string after trim.
 */
function trimNickNameForApi<T extends PayloadWithOptionalNames>(payload: T): void {
  if (payload.nickName === undefined) {
    return;
  }
  const t = payload.nickName.trim();
  (payload as Record<string, string>)["nickName"] = t.length === 0 ? "" : t;
}

/**
 * Before PUT: trim display name; drop empty display so the API treats it as unchanged.
 * Trim nickname; keep `nickName: ""` when cleared so the API clears nickname.
 */
export function sanitizeOptionalProfileNameFieldsForApi<
  T extends AdminUpdateUserInput | UpdateUserProfileInput,
>(payload: T): T {
  const next = { ...payload };
  trimOrDropKey(next, "displayName");
  trimNickNameForApi(next);
  return next;
}
