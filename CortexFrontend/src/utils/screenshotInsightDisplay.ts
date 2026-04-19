/**
 * Frontend-only filtering to de-emphasize low-signal lines in screenshot insight lists.
 * Does not change API contracts or AI output on the server.
 */
export function filterScreenshotInsightNoise(lines: string[]): string[] {
  return lines.map((l) => l.trim()).filter((line) => {
    if (!line) {
      return false;
    }
    if (isLowValueInsightLine(line)) {
      return false;
    }
    return true;
  });
}

function isLowValueInsightLine(line: string): boolean {
  const t = line.trim();
  const lower = t.toLowerCase();

  // Obvious UI chrome with no diagnostic value for reviewers
  if (
    /\bpassword\b/.test(lower) &&
    /\b(obscured|masked|hidden|not\s+visible|dots|asterisks)\b/.test(lower)
  ) {
    return true;
  }

  if (
    /^(the\s+)?password\s+field\s+is\s+(obscured|masked|hidden)\.?$/.test(lower)
  ) {
    return true;
  }

  return false;
}
