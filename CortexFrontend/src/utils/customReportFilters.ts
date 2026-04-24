export type CustomReportColumnFilterKind = "text" | "select" | "owner" | "date";

/** Select value that matches empty / null / whitespace-only cells. */
export const UNASSIGNED_FILTER = "__cortex_unassigned__";

export function getCustomReportColumnFilterKind(
  columnLabel: string,
): CustomReportColumnFilterKind {
  const n = columnLabel.trim().toLowerCase();
  if (n === "syniti owner" || n === "business owner") {
    return "owner";
  }
  if (
    n === "status" ||
    n === "priority" ||
    n === "board" ||
    n === "sla status" ||
    n === "approval status" ||
    n === "archive status"
  ) {
    return "select";
  }
  if (n.includes("date")) {
    return "date";
  }
  return "text";
}

export interface ColumnDistinct {
  values: string[];
  hasBlank: boolean;
}

export function computeColumnDistincts(
  columns: string[],
  rows: ReadonlyArray<Record<string, unknown>>,
): Record<string, ColumnDistinct> {
  const out: Record<string, ColumnDistinct> = {};
  for (const col of columns) {
    const kind = getCustomReportColumnFilterKind(col);
    if (kind !== "select" && kind !== "owner") {
      continue;
    }
    const set = new Set<string>();
    let hasBlank = false;
    for (const row of rows) {
      const raw = row[col];
      if (raw === null || raw === undefined) {
        hasBlank = true;
        continue;
      }
      const s = String(raw).trim();
      if (s === "") {
        hasBlank = true;
        continue;
      }
      set.add(s);
    }
    out[col] = {
      values: [...set].sort((a, b) =>
        a.localeCompare(b, undefined, { sensitivity: "base" }),
      ),
      hasBlank,
    };
  }
  return out;
}

function normalizeCellForCompare(cell: unknown): string {
  if (cell === null || cell === undefined) {
    return "";
  }
  return String(cell).trim();
}

function cellEqualsSelectOption(cell: unknown, option: string): boolean {
  return (
    normalizeCellForCompare(cell).localeCompare(option, undefined, {
      sensitivity: "base",
    }) === 0
  );
}

export function rowMatchesCustomReportFilters(
  row: Record<string, unknown>,
  columns: string[],
  columnFilterValues: Readonly<Record<string, string>>,
  globalNeedle: string,
): boolean {
  const g = globalNeedle.trim().toLowerCase();
  if (g) {
    const hit = columns.some((col) => {
      const cell = row[col];
      if (cell === null || cell === undefined) {
        return false;
      }
      return String(cell).toLowerCase().includes(g);
    });
    if (!hit) {
      return false;
    }
  }

  for (const col of columns) {
    const raw = columnFilterValues[col];
    if (raw === undefined || raw === null || raw === "") {
      continue;
    }
    const kind = getCustomReportColumnFilterKind(col);
    const cell = row[col];

    if (kind === "select" || kind === "owner") {
      if (raw === UNASSIGNED_FILTER) {
        if (normalizeCellForCompare(cell) !== "") {
          return false;
        }
      } else if (!cellEqualsSelectOption(cell, raw)) {
        return false;
      }
      continue;
    }

    const needle = raw.trim().toLowerCase();
    if (!needle) {
      continue;
    }
    const hay = String(cell ?? "").toLowerCase();
    if (!hay.includes(needle)) {
      return false;
    }
  }
  return true;
}

export function hasAnyCustomReportFilter(
  globalNeedle: string,
  columnFilterValues: Readonly<Record<string, string>>,
): boolean {
  if (globalNeedle.trim() !== "") {
    return true;
  }
  return Object.values(columnFilterValues).some(
    (v) => v !== undefined && v !== null && String(v).trim() !== "",
  );
}
