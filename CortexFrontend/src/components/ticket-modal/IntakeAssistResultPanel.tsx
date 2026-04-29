import {
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import {
  CLARITY_STATE_LABEL,
  CLARITY_STATE_PILL_CLASS,
  type ClarityState,
  type IntakeAssistResult,
} from "../../types/intakeAssist";
import { IntakeDraftPreview } from "./IntakeDraftPreview";

export function getIntakeAssistResultFingerprint(result: IntakeAssistResult): string {
  return [
    result.clarityState,
    result.improvedDescription ?? "",
    result.guidanceMessage ?? "",
    result.suggestedSummary ?? "",
    result.missingDetails.join("\u001e"),
  ].join("\u0000");
}

/**
 * Inline panel rendered under the Description textarea while in create mode.
 * Intentionally scoped so it shares intake vocabulary with TicketModal and
 * never becomes a modal-in-modal.
 */
export interface IntakeAssistResultPanelProps {
  result: IntakeAssistResult;
  editableDescription: string;
  onChangeEditableDescription: (value: string) => void;
  onUseSummary: () => void;
  onUseDescription: () => void;
  onDismiss: () => void;
}

export function IntakeAssistResultPanel({
  result,
  editableDescription,
  onChangeEditableDescription,
  onUseSummary,
  onUseDescription,
  onDismiss,
}: IntakeAssistResultPanelProps) {
  const [draftTab, setDraftTab] = useState<"preview" | "edit">("preview");
  const [panelExpanded, setPanelExpanded] = useState(true);
  const draftTextareaRef = useRef<HTMLTextAreaElement>(null);
  const clarityState: ClarityState = result.clarityState;
  const pillClass = CLARITY_STATE_PILL_CLASS[clarityState];
  const pillLabel = CLARITY_STATE_LABEL[clarityState];
  const hasSummary = Boolean(result.suggestedSummary?.trim());
  const hasEditableDescription = editableDescription.trim().length > 0;
  const hasMissingDetails = result.missingDetails.length > 0;

  const draftTextareaRows = useMemo(() => {
    const lines = editableDescription.split("\n").length;
    return Math.min(18, Math.max(8, lines + 2));
  }, [editableDescription]);

  useLayoutEffect(() => {
    if (draftTab === "edit") {
      draftTextareaRef.current?.focus();
    }
  }, [draftTab]);

  if (result.unavailable) {
    return (
      <div
        className="mt-3 rounded-md border border-amber-200 bg-amber-50 p-3.5 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-900/20 dark:text-amber-100"
        role="status"
      >
        <div className="flex items-start justify-between gap-3">
          <p>
            {result.unavailableReason?.trim() ||
              "Improve for review is not ready right now. Your draft remains unchanged."}
          </p>
          <button
            type="button"
            onClick={onDismiss}
            className="shrink-0 rounded-md border border-amber-300 bg-white px-2 py-1 text-xs font-semibold text-amber-900 hover:bg-amber-100 dark:border-amber-700 dark:bg-amber-900/30 dark:text-amber-100 dark:hover:bg-amber-900/50"
          >
            Dismiss
          </button>
        </div>
      </div>
    );
  }

  const collapsedSummaryLine = hasMissingDetails
    ? `${result.missingDetails.length} detail${result.missingDetails.length === 1 ? "" : "s"} missing for review`
    : "Reviewer-ready draft available";

  return (
    <div
      className="mt-3 rounded-md border border-gray-200 bg-gray-50 p-3.5 text-sm text-gray-800 dark:border-slate-700 dark:bg-slate-900/50 dark:text-slate-200"
      aria-live="polite"
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <button
          type="button"
          onClick={(e) => {
            e.preventDefault();
            e.stopPropagation();
            setPanelExpanded((open) => !open);
          }}
          className="relative z-10 flex min-w-0 flex-1 cursor-pointer items-start gap-2 rounded-md py-0.5 text-left transition-colors hover:bg-gray-50/80 dark:hover:bg-slate-800/50"
        >
          <div className="flex min-w-0 flex-1 flex-col gap-1.5 sm:flex-row sm:items-center sm:gap-2.5">
            <span
              className={`inline-flex w-fit shrink-0 items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${pillClass}`}
            >
              {pillLabel}
            </span>
            <span className="block min-w-0 text-xs leading-snug text-gray-600 dark:text-slate-400">
              {panelExpanded ? (
                <span className="font-medium text-gray-700 dark:text-slate-300">
                  View structured recommendation
                </span>
              ) : (
                <>
                  {collapsedSummaryLine}
                  <span className="text-gray-500 dark:text-slate-500">
                    {" "}
                    · tap to expand
                  </span>
                </>
              )}
            </span>
          </div>
          <span
            className="mt-0.5 shrink-0 text-[0.65rem] leading-none text-gray-400 dark:text-slate-500"
            aria-hidden="true"
          >
            {panelExpanded ? "▼" : "▶"}
          </span>
        </button>
        <button
          type="button"
          onClick={onDismiss}
          className="shrink-0 rounded-md border border-gray-300 bg-white px-2 py-1 text-xs font-semibold text-gray-700 hover:bg-gray-100 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
        >
          Dismiss
        </button>
      </div>

      <div
        id="ticket-intake-assist-panel-body"
        className="mt-3 space-y-3.5 border-t border-slate-200/60 pt-3.5 dark:border-slate-600/45"
        role="region"
        aria-label="Structured intake recommendation"
        hidden={!panelExpanded}
      >
        {panelExpanded ? (
          <>
          <p className="text-xs text-gray-500 dark:text-slate-400">
            Cortex suggestion — apply when you're ready.
          </p>

          {result.guidanceMessage && (
            <p className="text-sm leading-relaxed text-gray-700 dark:text-slate-300">
              {result.guidanceMessage}
            </p>
          )}

          {hasSummary && (
            <div className="rounded-md border border-gray-200 bg-white p-3.5 dark:border-slate-700 dark:bg-slate-900">
              <div className="mb-2 flex items-center justify-between gap-2">
                <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                  Suggested title line
                </p>
                <button
                  type="button"
                  onClick={onUseSummary}
                  className="rounded-md border border-cortex-blue/40 bg-white px-2 py-1 text-xs font-semibold text-cortex-blue hover:bg-cortex-blue/10 dark:border-cortex-blue/50 dark:bg-slate-900 dark:text-cortex-blue dark:hover:bg-slate-800"
                >
                  Use this
                </button>
              </div>
              <p className="text-sm leading-relaxed text-gray-800 dark:text-slate-100">
                {result.suggestedSummary}
              </p>
            </div>
          )}

          {result.improvedDescription != null && (
            <div className="rounded-md border border-gray-200 bg-white p-3.5 dark:border-slate-700 dark:bg-slate-900">
              <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
                <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                  Reviewer-ready description
                </p>
                <button
                  type="button"
                  onClick={onUseDescription}
                  disabled={!hasEditableDescription}
                  className="rounded-md border border-cortex-blue/40 bg-white px-2 py-1 text-xs font-semibold text-cortex-blue hover:bg-cortex-blue/10 disabled:cursor-not-allowed disabled:opacity-60 dark:border-cortex-blue/50 dark:bg-slate-900 dark:text-cortex-blue dark:hover:bg-slate-800"
                >
                  Use this description
                </button>
              </div>

              <div
                className="mb-2 flex rounded-lg bg-slate-100/90 p-1 dark:bg-slate-800/80"
                role="tablist"
                aria-label="Draft view"
              >
                <button
                  type="button"
                  id="ticket-intake-draft-preview-tab"
                  role="tab"
                  aria-selected={draftTab === "preview" ? "true" : "false"}
                  aria-controls="ticket-intake-draft-preview-panel"
                  tabIndex={draftTab === "preview" ? 0 : -1}
                  className={`flex-1 rounded-md px-2.5 py-1.5 text-xs font-semibold transition-colors ${
                    draftTab === "preview"
                      ? "bg-white text-gray-900 shadow-sm dark:bg-slate-900 dark:text-slate-100"
                      : "text-gray-600 hover:text-gray-900 dark:text-slate-400 dark:hover:text-slate-200"
                  }`}
                  onClick={() => setDraftTab("preview")}
                >
                  Preview
                </button>
                <button
                  type="button"
                  id="ticket-intake-draft-edit-tab"
                  role="tab"
                  aria-selected={draftTab === "edit" ? "true" : "false"}
                  aria-controls="ticket-intake-draft-edit-panel"
                  tabIndex={draftTab === "edit" ? 0 : -1}
                  className={`flex-1 rounded-md px-2.5 py-1.5 text-xs font-semibold transition-colors ${
                    draftTab === "edit"
                      ? "bg-white text-gray-900 shadow-sm dark:bg-slate-900 dark:text-slate-100"
                      : "text-gray-600 hover:text-gray-900 dark:text-slate-400 dark:hover:text-slate-200"
                  }`}
                  onClick={() => setDraftTab("edit")}
                >
                  Edit
                </button>
              </div>
              <p className="mb-2 text-xs leading-snug text-gray-500 dark:text-slate-500">
                Preview shows structure and line breaks. Use Edit to change the
                text.
              </p>

              <IntakeDraftPreview
                text={editableDescription}
                hidden={draftTab !== "preview"}
              />
              <div
                id="ticket-intake-draft-edit-panel"
                role="tabpanel"
                aria-labelledby="ticket-intake-draft-edit-tab"
                className="min-w-0"
                hidden={draftTab !== "edit"}
              >
                <textarea
                  ref={draftTextareaRef}
                  value={editableDescription}
                  onChange={(e) =>
                    onChangeEditableDescription(e.target.value)
                  }
                  rows={draftTextareaRows}
                  spellCheck
                  className="w-full resize-y rounded-lg border border-slate-200/90 bg-gradient-to-b from-white to-slate-50/40 px-4 py-3.5 text-[0.9375rem] leading-relaxed text-gray-900 shadow-sm placeholder:text-gray-400 focus:border-cortex-blue focus:outline-none focus:ring-2 focus:ring-cortex-blue/30 dark:border-slate-600/70 dark:from-slate-950 dark:to-slate-950/80 dark:text-slate-100 dark:placeholder:text-slate-500 dark:focus:ring-cortex-blue/25"
                />
              </div>
            </div>
          )}

          {hasMissingDetails && (
            <div className="rounded-md border border-gray-200 bg-white p-3.5 dark:border-slate-700 dark:bg-slate-900">
              <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                {clarityState === "ready_for_execution"
                  ? "What could help (optional)"
                  : "What's missing"}
              </p>
              <ul className="list-outside list-disc space-y-2.5 pl-5 text-sm leading-relaxed text-gray-800 dark:text-slate-100">
                {result.missingDetails.map((detail, index) => (
                  <li key={`${index}-${detail}`}>{detail}</li>
                ))}
              </ul>
            </div>
          )}
          </>
        ) : null}
      </div>
    </div>
  );
}
