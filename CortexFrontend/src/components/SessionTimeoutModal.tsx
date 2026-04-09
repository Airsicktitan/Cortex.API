interface SessionTimeoutModalProps {
  state: "warning" | "expired" | null;
  remainingSeconds: number;
  inactivityTimeoutMinutes: number;
  onContinue: () => void;
  onReauthenticate: () => void;
}

function formatRemainingTime(remainingSeconds: number) {
  const clampedSeconds = Math.max(0, remainingSeconds);
  const minutes = Math.floor(clampedSeconds / 60);
  const seconds = clampedSeconds % 60;
  return `${minutes}:${String(seconds).padStart(2, "0")}`;
}

export default function SessionTimeoutModal({
  state,
  remainingSeconds,
  inactivityTimeoutMinutes,
  onContinue,
  onReauthenticate,
}: SessionTimeoutModalProps) {
  if (!state) {
    return null;
  }

  const isWarning = state === "warning";

  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center bg-black/55 px-4 py-6 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-2xl border border-gray-200 bg-white p-6 shadow-2xl dark:border-slate-800 dark:bg-slate-900">
        <div className="flex items-start gap-4">
          <div
            className={`mt-1 inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-lg ${
              isWarning
                ? "bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300"
                : "bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-300"
            }`}
          >
            {isWarning ? "!" : "⏳"}
          </div>

          <div className="min-w-0 flex-1">
            <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
              {isWarning
                ? "Session expiring soon"
                : "Session expired due to inactivity"}
            </h2>
            <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">
              {isWarning
                ? `For security, CORTEX requires re-authentication after ${inactivityTimeoutMinutes} minutes of inactivity.`
                : "For security, your session is now locked until you sign in again."}
            </p>
            {isWarning ? (
              <p className="mt-4 rounded-lg bg-amber-50 px-4 py-3 text-sm font-medium text-amber-800 dark:bg-amber-950/30 dark:text-amber-200">
                Re-authentication required in {formatRemainingTime(remainingSeconds)}
              </p>
            ) : (
              <p className="mt-4 rounded-lg bg-red-50 px-4 py-3 text-sm font-medium text-red-700 dark:bg-red-950/30 dark:text-red-200">
                Please re-authenticate to continue working.
              </p>
            )}
          </div>
        </div>

        <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          {isWarning && (
            <button
              type="button"
              onClick={onContinue}
              className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            >
              Continue Working
            </button>
          )}
          <button
            type="button"
            onClick={onReauthenticate}
            className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
          >
            Re-authenticate
          </button>
        </div>
      </div>
    </div>
  );
}
