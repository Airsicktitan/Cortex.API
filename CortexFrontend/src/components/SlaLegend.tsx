const legendItems = [
  {
    label: "In SLA",
    description: "More than the warning window remains.",
    colorClass: "bg-green-500",
  },
  {
    label: "Approaching SLA",
    description: "Inside the warning window before breach.",
    colorClass: "bg-yellow-400",
  },
  {
    label: "Outside SLA",
    description: "Past the configured SLA deadline.",
    colorClass: "bg-red-500",
  },
];

interface SlaLegendProps {
  compact?: boolean;
}

export default function SlaLegend({ compact = false }: SlaLegendProps) {
  if (compact) {
    return (
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <span className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          SLA Legend
        </span>
        {legendItems.map((item) => (
          <div
            key={item.label}
            className="flex items-center gap-2 text-sm text-gray-700 dark:text-slate-300"
            title={item.description}
          >
            <span className={`h-3 w-3 rounded-full ${item.colorClass}`} />
            <span>{item.label}</span>
          </div>
        ))}
      </div>
    );
  }

  return (
    <div className="h-full rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
        <div>
          <h3 className="text-sm font-semibold text-gray-900 dark:text-slate-100">SLA Legend</h3>
          <p className="text-sm text-gray-500 dark:text-slate-400">
            Ticket card borders reflect the current SLA state.
          </p>
        </div>

        <div className="flex flex-wrap gap-4">
          {legendItems.map((item) => (
            <div
              key={item.label}
              className="flex items-center gap-2 text-sm text-gray-700 dark:text-slate-300"
              title={item.description}
            >
              <span className={`h-3 w-3 rounded-full ${item.colorClass}`} />
              <span>{item.label}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
