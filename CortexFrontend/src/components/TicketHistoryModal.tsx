import { useEffect, useRef, useState } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import { getUserFacingErrorMessage, ticketService } from "../services/api";
import type { TicketAuditEntry } from "../types/ticketAudit";
import { formatDisplayDateTime, formatDisplayValue } from "../utils/presentation";
import { ScrollToBottomButton } from "./ui/ScrollToBottomButton";

const API_AUDIENCE = "https://cortex-api";

interface TicketHistoryModalProps {
  ticketId: string;
  isOpen: boolean;
  onClose: () => void;
}

function getActionBadgeClass(action: string) {
  switch (action) {
    case "Created":
      return "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200";
    case "Updated":
      return "bg-cortex-blue-soft text-cortex-ink dark:bg-cortex-blue/20 dark:text-cortex-cyan";
    case "Archived":
      return "bg-amber-100 text-amber-800 dark:bg-amber-950/30 dark:text-amber-200";
    case "Reactivated":
      return "bg-violet-100 text-violet-800 dark:bg-violet-950/30 dark:text-violet-200";
    case "CommentAdded":
      return "bg-cyan-100 text-cyan-800 dark:bg-cyan-950/30 dark:text-cyan-200";
    case "AttachmentAdded":
      return "bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-200";
    default:
      return "bg-gray-100 text-gray-700 dark:bg-slate-800 dark:text-slate-200";
  }
}

export default function TicketHistoryModal({
  ticketId,
  isOpen,
  onClose,
}: TicketHistoryModalProps) {
  const { getAccessTokenSilently } = useAuth0();
  const [history, setHistory] = useState<TicketAuditEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const historyScrollRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    let cancelled = false;

    const loadHistory = async () => {
      setLoading(true);
      setError(null);

      try {
        const token = await getAccessTokenSilently({
          authorizationParams: {
            audience: API_AUDIENCE,
          },
        });
        const entries = await ticketService.getHistory(ticketId, token);

        if (cancelled) {
          return;
        }

        setHistory(entries);
      } catch (loadError) {
        console.error("Failed to load ticket history", loadError);

        if (cancelled) {
          return;
        }

        setError(
          getUserFacingErrorMessage(loadError, "Unable to load ticket history."),
        );
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void loadHistory();

    return () => {
      cancelled = true;
    };
  }, [getAccessTokenSilently, isOpen, ticketId]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) {
    return null;
  }

  return (
    <div className="scroll-surface fixed inset-0 z-[60] overflow-y-auto">
      <div
        className="fixed inset-0 bg-black/50 transition-opacity"
        onClick={onClose}
      />

      <div className="flex min-h-full items-start justify-center p-3 sm:items-center sm:p-4">
        <div className="relative max-h-[min(90dvh,85vh)] w-full max-w-4xl overflow-hidden rounded-lg border border-gray-200 bg-white text-gray-900 shadow-xl dark:border-slate-800 dark:bg-slate-900 dark:text-slate-100">
          <div className="flex items-start justify-between gap-3 border-b border-gray-100 px-4 py-4 dark:border-slate-800 sm:px-6 sm:py-5">
            <div className="min-w-0 pr-2">
              <h2 className="text-lg font-semibold sm:text-xl">Audit History</h2>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Review who changed this ticket, what changed, and when it happened.
              </p>
            </div>

            <button
              onClick={onClose}
              className="text-2xl font-bold text-gray-400 transition-colors hover:text-gray-600 dark:text-slate-500 dark:hover:text-slate-300"
            >
              ×
            </button>
          </div>

          <div className="relative">
            <div
              ref={historyScrollRef}
              className="scroll-surface max-h-[calc(min(90dvh,85vh)-5.5rem)] overflow-y-auto px-4 py-4 sm:px-6 sm:py-5"
            >
              {loading ? (
                <div className="space-y-4">
                  {Array.from({ length: 4 }).map((_, index) => (
                    <div
                      key={index}
                      className="animate-pulse rounded-lg border border-gray-200 bg-gray-50 p-5 dark:border-slate-800 dark:bg-slate-950/40"
                    >
                      <div className="flex items-center justify-between gap-4">
                        <div className="h-5 w-40 rounded bg-gray-200 dark:bg-slate-800" />
                        <div className="h-6 w-20 rounded-full bg-gray-200 dark:bg-slate-800" />
                      </div>
                      <div className="mt-4 h-4 w-64 rounded bg-gray-200 dark:bg-slate-800" />
                      <div className="mt-3 h-4 w-full rounded bg-gray-200 dark:bg-slate-800" />
                      <div className="mt-2 h-4 w-5/6 rounded bg-gray-200 dark:bg-slate-800" />
                    </div>
                  ))}
                </div>
              ) : error ? (
                <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
                  <p className="text-red-700 dark:text-red-300">{error}</p>
                </div>
              ) : history.length === 0 ? (
                <div className="rounded-lg border border-gray-200 bg-gray-50 px-6 py-12 text-center text-gray-500 dark:border-slate-800 dark:bg-slate-950/40 dark:text-slate-400">
                  No audit history has been recorded for this ticket yet.
                </div>
              ) : (
                <div className="space-y-4">
                  {history.map((entry) => (
                    <section
                      key={entry.id}
                      className="rounded-lg border border-gray-200 bg-gray-50 p-5 dark:border-slate-800 dark:bg-slate-950/40"
                    >
                    <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                      <div>
                        <div className="flex flex-wrap items-center gap-3">
                          <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
                            {entry.summary}
                          </h3>
                          <span
                            className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${getActionBadgeClass(entry.action)}`}
                          >
                            {entry.action}
                          </span>
                        </div>
                        <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
                          {formatDisplayValue(entry.changedByDisplayName)} ·{" "}
                          {formatDisplayDateTime(entry.changedDateUtc)}
                        </p>
                      </div>
                    </div>

                    {entry.reason && (
                      <div className="mt-4 rounded-md border border-cortex-blue/30 bg-cortex-blue-soft px-4 py-3 text-sm text-cortex-ink dark:border-cortex-blue/30 dark:bg-cortex-blue/20 dark:text-slate-100">
                        <span className="font-medium">Reason:</span> {entry.reason}
                      </div>
                    )}

                    {entry.fieldChanges.length > 0 && (
                      <div className="mt-4 overflow-x-auto rounded-md border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
                        <table className="min-w-[42rem] text-sm">
                          <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                            <tr>
                              <th className="px-4 py-3 font-medium">Field</th>
                              <th className="px-4 py-3 font-medium">Previous</th>
                              <th className="px-4 py-3 font-medium">Updated</th>
                            </tr>
                          </thead>
                          <tbody>
                            {entry.fieldChanges.map((change, index) => (
                              <tr
                                key={`${entry.id}-${change.fieldName}-${index}`}
                                className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                              >
                                <td className="px-4 py-3 align-top font-medium text-gray-900 dark:text-slate-100">
                                  {change.fieldName}
                                </td>
                                <td className="px-4 py-3 align-top whitespace-pre-wrap break-words text-gray-500 dark:text-slate-400">
                                  {formatDisplayValue(change.oldValue)}
                                </td>
                                <td className="px-4 py-3 align-top whitespace-pre-wrap break-words">
                                  {formatDisplayValue(change.newValue)}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                    </section>
                  ))}
                </div>
              )}
            </div>
            <ScrollToBottomButton
              containerRef={historyScrollRef}
              aria-label="Scroll audit history to bottom"
            />
          </div>
        </div>
      </div>
    </div>
  );
}
