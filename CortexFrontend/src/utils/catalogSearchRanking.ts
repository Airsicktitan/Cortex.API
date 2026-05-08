/**
 * Catalog search ranking for Configuration visibility (mirror of CortexBackend.Services.CatalogSearchRanking).
 * Keep behavior aligned when updating either side.
 */

import type { SapReferenceCatalogEntry } from "../types/sapReferenceCatalog";
import type { SynitiKnowledgeCatalogEntry } from "../types/synitiKnowledgeCatalog";

const delimiterPattern = /[|;\n\r]+/g;

export function normalizeSearchText(input?: string | null): string {
  if (!input?.trim()) {
    return "";
  }
  return input
    .trim()
    .toLowerCase()
    .replace(/\s+/g, " ");
}

function splitPhrases(raw: string | null | undefined): string[] {
  if (!raw?.trim()) {
    return [];
  }
  const parts: string[] = [];
  for (const segment of raw.split(delimiterPattern)) {
    const n = normalizeSearchText(segment);
    if (n.length > 0) {
      parts.push(n);
    }
  }
  return parts;
}

function phraseListContainsSubstring(raw: string | null | undefined, q: string): boolean {
  if (!raw?.trim()) {
    return false;
  }
  for (const phrase of splitPhrases(raw)) {
    if (phrase !== q && phrase.includes(q)) {
      return true;
    }
  }
  return false;
}

function containsNormalized(text: string | null | undefined, q: string): boolean {
  if (!text?.trim()) {
    return false;
  }
  return normalizeSearchText(text).includes(q);
}

function guidanceOrRelatedContains(entry: SynitiKnowledgeCatalogEntry, q: string): boolean {
  if (
    containsNormalized(entry.businessMeaning, q) ||
    containsNormalized(entry.shortDefinition, q) ||
    containsNormalized(entry.technicalMeaning, q) ||
    containsNormalized(entry.relatedTerms, q)
  ) {
    return true;
  }
  for (const line of entry.suggestedReviewerChecks) {
    if (normalizeSearchText(line).includes(q)) {
      return true;
    }
  }
  for (const line of entry.missingContextQuestions) {
    if (normalizeSearchText(line).includes(q)) {
      return true;
    }
  }
  return false;
}

export function getSynitiSortKey(entry: SynitiKnowledgeCatalogEntry, normalizedQuery: string): number {
  if (normalizedQuery.length === 0) {
    return 0;
  }

  const term = normalizeSearchText(entry.term);
  const category = normalizeSearchText(entry.category);
  let best = Number.MAX_SAFE_INTEGER;

  if (term === normalizedQuery) {
    best = Math.min(best, 100);
  }
  if (term.startsWith(normalizedQuery)) {
    best = Math.min(best, 200);
  }
  if (category === normalizedQuery) {
    best = Math.min(best, 300);
  }
  for (const phrase of splitPhrases(entry.aliases)) {
    if (phrase === normalizedQuery) {
      best = Math.min(best, 300);
    }
  }
  for (const phrase of splitPhrases(entry.examplePhrases)) {
    if (phrase === normalizedQuery) {
      best = Math.min(best, 300);
    }
  }
  if (term !== normalizedQuery && !term.startsWith(normalizedQuery) && term.includes(normalizedQuery)) {
    best = Math.min(best, 400);
  }
  if (
    phraseListContainsSubstring(entry.aliases, normalizedQuery) ||
    phraseListContainsSubstring(entry.examplePhrases, normalizedQuery)
  ) {
    best = Math.min(best, 500);
  }
  if (
    containsNormalized(entry.aliases, normalizedQuery) ||
    containsNormalized(entry.examplePhrases, normalizedQuery)
  ) {
    best = Math.min(best, 500);
  }
  if (guidanceOrRelatedContains(entry, normalizedQuery)) {
    best = Math.min(best, 600);
  }

  return best === Number.MAX_SAFE_INTEGER ? 650 : best;
}

function contextExactMatch(entry: SapReferenceCatalogEntry, q: string): boolean {
  const m = normalizeSearchText(entry.module);
  const d = normalizeSearchText(entry.domain);
  const b = normalizeSearchText(entry.businessObject);
  return (m.length > 0 && m === q) || (d.length > 0 && d === q) || (b.length > 0 && b === q);
}

function contextSubstringMatch(entry: SapReferenceCatalogEntry, q: string): boolean {
  for (const p of [entry.module, entry.domain, entry.businessObject]) {
    const n = normalizeSearchText(p);
    if (n.length > 0 && n.includes(q)) {
      return true;
    }
  }
  return false;
}

function descriptionOrContextContains(entry: SapReferenceCatalogEntry, q: string): boolean {
  return (
    containsNormalized(entry.tableDescription, q) ||
    containsNormalized(entry.fieldDescription, q) ||
    contextSubstringMatch(entry, q)
  );
}

function sourceFieldsContain(entry: SapReferenceCatalogEntry, q: string): boolean {
  return containsNormalized(entry.sourceName, q) || containsNormalized(entry.sourceType, q);
}

export function getSapSortKey(entry: SapReferenceCatalogEntry, normalizedQuery: string): number {
  if (normalizedQuery.length === 0) {
    return 0;
  }

  const table = normalizeSearchText(entry.tableName);
  const field = entry.fieldName ? normalizeSearchText(entry.fieldName) : "";
  const isTable = entry.rowKind.toLowerCase() === "table";

  let best = Number.MAX_SAFE_INTEGER;

  if (isTable && table === normalizedQuery) {
    best = Math.min(best, 100);
  }
  if (!isTable && field === normalizedQuery) {
    best = Math.min(best, 110);
  }
  if (!isTable && table === normalizedQuery && field.length > 0 && field !== normalizedQuery) {
    best = Math.min(best, 120);
  }

  const startsTable = table.startsWith(normalizedQuery) && table !== normalizedQuery;
  const startsField = field.length > 0 && field.startsWith(normalizedQuery) && field !== normalizedQuery;
  if (startsTable || startsField) {
    const sub = isTable ? 0 : 1;
    best = Math.min(best, 200 + sub);
  }

  if (contextExactMatch(entry, normalizedQuery)) {
    best = Math.min(best, 300);
  }

  const nameContains =
    (table.includes(normalizedQuery) && table !== normalizedQuery && !table.startsWith(normalizedQuery)) ||
    (field.length > 0 &&
      field.includes(normalizedQuery) &&
      field !== normalizedQuery &&
      !field.startsWith(normalizedQuery));
  if (nameContains) {
    best = Math.min(best, 400);
  }

  if (descriptionOrContextContains(entry, normalizedQuery)) {
    best = Math.min(best, 500);
  }
  if (sourceFieldsContain(entry, normalizedQuery)) {
    best = Math.min(best, 600);
  }

  return best;
}
