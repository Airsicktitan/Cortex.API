import type { IntakeAssistResult } from "../types/intakeAssist";

export function getIntakeAssistResultFingerprint(result: IntakeAssistResult): string {
  return [
    result.clarityState,
    result.improvedDescription ?? "",
    result.guidanceMessage ?? "",
    result.suggestedSummary ?? "",
    result.missingDetails.join("\u001e"),
  ].join("\u0000");
}
