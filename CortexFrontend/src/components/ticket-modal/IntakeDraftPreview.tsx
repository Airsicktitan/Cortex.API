/** Renders assist draft with preserved breaks; highlights common section labels and bullets for scanning. */
export function IntakeDraftPreview({
  text,
  hidden,
}: {
  text: string;
  hidden?: boolean;
}) {
  if (!text.trim()) {
    return (
      <p
        id="ticket-intake-draft-preview-panel"
        role="tabpanel"
        aria-labelledby="ticket-intake-draft-preview-tab"
        hidden={hidden}
        className="text-sm italic text-gray-500 dark:text-slate-400"
      >
        No text yet.
      </p>
    );
  }

  const lines = text.split("\n");
  const sectionRe =
    /^\s*(Issue|What happened|Impact|Notes|Important):\s*(.*)$/i;

  return (
    <div
      id="ticket-intake-draft-preview-panel"
      className="max-h-[min(22rem,45vh)] overflow-y-auto rounded-lg border border-slate-200/80 bg-gradient-to-b from-white to-slate-50/50 px-5 py-4 text-sm text-gray-800 shadow-[inset_0_1px_0_0_rgba(255,255,255,0.65)] dark:border-slate-600/55 dark:from-slate-900/95 dark:to-slate-950/90 dark:text-slate-200 dark:shadow-none"
      role="tabpanel"
      aria-labelledby="ticket-intake-draft-preview-tab"
      hidden={hidden}
    >
      <div className="select-text">
        {lines.map((line, i) => {
          const sectionMatch = line.match(sectionRe);
          if (sectionMatch) {
            const title = sectionMatch[1] ?? "";
            const rest = (sectionMatch[2] ?? "").trim();
            const hasPriorSection = lines
              .slice(0, i)
              .some((l) => l.match(sectionRe));
            /** Space between major sections; spacing-first (no heavy dividers). */
            const sectionBreak = hasPriorSection ? "mt-8" : "";

            if (!rest) {
              return (
                <p
                  key={`${i}-${title}`}
                  className={`${sectionBreak} mb-2.5 text-sm font-semibold tracking-tight text-gray-900 dark:text-slate-100`}
                >
                  {title}:
                </p>
              );
            }
            return (
              <div
                key={`${i}-${title}`}
                className={`${sectionBreak} space-y-2.5`}
              >
                <p className="text-sm font-semibold tracking-tight text-gray-900 dark:text-slate-100">
                  {title}:
                </p>
                <p className="text-[0.9375rem] leading-[1.7] text-gray-800 dark:text-slate-200">
                  {rest}
                </p>
              </div>
            );
          }

          const bulletMatch = line.match(/^\s*[-•]\s+(.+)$/);
          if (bulletMatch) {
            return (
              <div
                key={i}
                className="ml-0.5 flex gap-2.5 border-l border-cortex-blue/22 py-1 pl-3.5 dark:border-cortex-blue/30"
              >
                <span
                  className="mt-[0.2rem] shrink-0 text-xs font-semibold text-cortex-blue/70 dark:text-cortex-blue/50"
                  aria-hidden="true"
                >
                  ·
                </span>
                <span className="text-[0.9375rem] leading-[1.7] text-gray-800 dark:text-slate-200">
                  {bulletMatch[1]}
                </span>
              </div>
            );
          }

          const numberedMatch = line.match(/^\s*(\d+)\.\s+(.+)$/);
          if (numberedMatch) {
            return (
              <div
                key={i}
                className="ml-0.5 flex gap-2 border-l border-slate-200/80 py-1 pl-3.5 dark:border-slate-600/55"
              >
                <span className="mt-[0.12rem] min-w-[1.25rem] shrink-0 text-xs tabular-nums text-gray-500 dark:text-slate-400">
                  {numberedMatch[1]}.
                </span>
                <span className="text-[0.9375rem] leading-[1.7] text-gray-800 dark:text-slate-200">
                  {numberedMatch[2]}
                </span>
              </div>
            );
          }

          if (line.trim() === "") {
            return (
              <div key={i} className="h-2 shrink-0" aria-hidden="true" />
            );
          }

          return (
            <p
              key={i}
              className="text-[0.9375rem] leading-[1.7] text-gray-800 dark:text-slate-200"
            >
              {line}
            </p>
          );
        })}
      </div>
    </div>
  );
}
