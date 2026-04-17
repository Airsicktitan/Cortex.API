export function formatDisplayValue(value?: string | null): string {
  if (!value) {
    return "—";
  }

  const trimmed = value.trim();
  if (!trimmed) {
    return "—";
  }

  const normalized = trimmed.toLowerCase();
  return normalized === "null" || normalized === "undefined" ? "—" : trimmed;
}

export function formatDisplayDateTime(value?: string | null): string {
  const normalized = formatDisplayValue(value);
  if (normalized === "—") {
    return "—";
  }

  const parsed = new Date(normalized);
  if (Number.isNaN(parsed.getTime())) {
    return "—";
  }

  return parsed.toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).replace(",", " \u00b7");
}

export function humanizeEnumLabel(value?: string | null): string {
  const normalized = formatDisplayValue(value);
  if (normalized === "—") {
    return "—";
  }

  return normalized
    .replace(/[_-]+/g, " ")
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/\s+/g, " ")
    .trim();
}

export function formatTicketIdentifier(id?: string | null): string {
  const normalized = formatDisplayValue(id);
  if (normalized === "—") {
    return "—";
  }

  if (/^\d+$/.test(normalized)) {
    return `TICKET-${normalized}`;
  }

  return normalized;
}
