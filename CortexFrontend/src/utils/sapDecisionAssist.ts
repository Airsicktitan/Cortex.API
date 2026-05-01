import type { SapTicketReferenceMatch } from "../types/sapTicketReference";
import {
  collectSapReferenceMetadataSignals,
  isCustomFieldSignal,
  type SapReferenceMetadataSignals,
} from "./sapReferenceMetadataSignals";

export type SapDecisionAssist = {
  impactLines: string[];
  readinessChecks: string[];
  reviewerFocus: string[];
};

function pushDedupe(arr: string[], seen: Set<string>, ...items: string[]) {
  for (const item of items) {
    const t = item.trim();
    if (!t || seen.has(t)) {
      continue;
    }
    seen.add(t);
    arr.push(t);
  }
}

function dedupeMerge(segments: string[][]): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const seg of segments) {
    for (const item of seg) {
      const t = item.trim();
      if (!t || seen.has(t)) {
        continue;
      }
      seen.add(t);
      out.push(t);
    }
  }
  return out;
}

const CAP_IMPACT = 2;
const CAP_READINESS = 3;
const CAP_FOCUS = 3;

function addTableReadinessAndFocus(
  sig: SapReferenceMetadataSignals,
  readinessSegments: string[][],
  focusSegments: string[][],
) {
  const { tables, hasCustomField } = sig;

  if (tables.has("MARC")) {
    readinessSegments.push([
      "Confirm affected plant(s).",
      "Confirm affected material numbers.",
      "Confirm material/plant extension scope.",
    ]);
    focusSegments.push([
      "Material Master / Supply Chain",
      "Plant-level material data owner",
    ]);
  }

  if (tables.has("MARA")) {
    readinessSegments.push([
      "Confirm affected material numbers.",
      "Confirm general material master data scope.",
    ]);
    focusSegments.push(["Material Master owner"]);
  }

  if (tables.has("LFA1")) {
    readinessSegments.push([
      "Confirm affected vendor or account numbers.",
      "Confirm vendor general data scope.",
    ]);
    focusSegments.push(["Vendor Master owner"]);
  }

  if (tables.has("KNA1")) {
    readinessSegments.push([
      "Confirm affected customer numbers.",
      "Confirm customer general data scope.",
    ]);
    focusSegments.push(["Customer Master owner"]);
  }

  if (tables.has("EINA") || tables.has("EINE")) {
    readinessSegments.push([
      "Confirm supplier, material, and purchasing organization scope.",
    ]);
  }

  if (tables.has("QMAT")) {
    readinessSegments.push([
      "Confirm material, plant, and inspection type scope.",
    ]);
  }

  if (tables.has("MARC") && hasCustomField) {
    readinessSegments.push([
      "Confirm whether plant/material scope is fully known before approval.",
    ]);
  }
}

/**
 * Metadata-driven decision support; table rules add secondary readiness/focus only.
 */
export function buildSapDecisionAssist(
  matches: SapTicketReferenceMatch[],
): SapDecisionAssist | null {
  if (!matches.length) {
    return null;
  }

  const sig = collectSapReferenceMetadataSignals(matches);

  const impact: string[] = [];
  const seenI = new Set<string>();
  const readinessSegments: string[][] = [];
  const focusSegments: string[][] = [];

  let firstCustom = sig.customFieldNames[0] ?? null;
  if (!firstCustom) {
    for (const m of matches) {
      if (isCustomFieldSignal(m) && m.fieldName?.trim()) {
        firstCustom = m.fieldName.trim();
        break;
      }
    }
  }

  if (firstCustom) {
    pushDedupe(
      impact,
      seenI,
      `Custom SAP field detected: ${firstCustom}.`,
    );
    readinessSegments.push([
      "Confirm the field exists in the project mapping or specification.",
      "Confirm whether the field is source-provided, transformed, defaulted, or SAP-maintained.",
    ]);
    focusSegments.push([
      "Mapping / transformation owner",
      "Validation or load rule owner",
    ]);
  }

  for (const bo of sig.businessObjects.slice(0, 2)) {
    if (impact.length >= CAP_IMPACT) {
      break;
    }
    pushDedupe(impact, seenI, `Ticket may reference ${bo} data.`);
  }

  if (impact.length < CAP_IMPACT && sig.sortedTables.length > 0) {
    const t = sig.sortedTables[0];
    const desc = sig.tableToDescription.get(t);
    if (desc) {
      pushDedupe(impact, seenI, `Ticket may reference ${t} — ${desc}.`);
    } else {
      pushDedupe(
        impact,
        seenI,
        `SAP table ${t} matched stored reference metadata.`,
      );
    }
  }

  if (impact.length < CAP_IMPACT) {
    const modPart =
      sig.modules.length > 0 ? `Module ${sig.modules.join(", ")}` : "";
    const domPart =
      sig.dataDomains.length > 0
        ? `domain ${sig.dataDomains.join(", ")}`
        : "";
    if (modPart && domPart) {
      pushDedupe(impact, seenI, `Ticket may reference ${modPart}; ${domPart}.`);
    } else if (modPart) {
      pushDedupe(impact, seenI, `Ticket may reference ${modPart} (SAP metadata).`);
    } else if (domPart) {
      pushDedupe(impact, seenI, `Ticket may reference ${domPart} scope (SAP metadata).`);
    }
  }

  if (impact.length === 0) {
    pushDedupe(
      impact,
      seenI,
      "SAP reference metadata was detected on this ticket.",
    );
  }

  readinessSegments.push([
    "Confirm affected records and organizational scope.",
    "Confirm mapping and validation/load rule coverage.",
  ]);

  if (!sig.hasCustomField) {
    readinessSegments.push([
      "Confirm whether the issue may reflect missing source data, mapping, or failed validation.",
    ]);
  }

  for (const bo of sig.businessObjects.slice(0, 2)) {
    focusSegments.push([`${bo} data owner`]);
  }

  for (const mod of sig.modules.slice(0, 2)) {
    focusSegments.push([`${mod} process/data owner`]);
  }

  focusSegments.push([
    "Mapping / transformation owner",
    "Validation or load rule owner",
  ]);

  if (sig.hasMaterialMasterBo) {
    focusSegments.push(["Material Master / Supply Chain"]);
  }

  for (const d of sig.dataDomains.slice(0, 1)) {
    focusSegments.push([`${d} governance owner`]);
  }

  addTableReadinessAndFocus(sig, readinessSegments, focusSegments);

  const readiness = dedupeMerge(readinessSegments);
  const reviewerFocus = dedupeMerge(focusSegments);

  return {
    impactLines: impact.slice(0, CAP_IMPACT),
    readinessChecks: readiness.slice(0, CAP_READINESS),
    reviewerFocus: reviewerFocus.slice(0, CAP_FOCUS),
  };
}
