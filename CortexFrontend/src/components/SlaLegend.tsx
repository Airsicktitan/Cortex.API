import { CortexTooltip } from "./ui/Tooltip";

const legendItems = [
  {
    label: "On Track",
    description: "On track to resolve before the SLA deadline.",
    colorClass: "bg-green-500",
  },
  {
    label: "At Risk",
    description:
      "Inside the warning window — likely to breach without action.",
    colorClass: "bg-yellow-400",
  },
  {
    label: "Overdue",
    description: "Past the SLA deadline and still open.",
    colorClass: "bg-red-500",
  },
  {
    label: "Resolved",
    description:
      "Closed. Badge shows Resolved On Time or Resolved Late.",
    colorClass: "bg-emerald-600",
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
          <CortexTooltip key={item.label} content={item.description}>
            <div className="flex cursor-help items-center gap-2 text-sm text-gray-700 dark:text-slate-300">
              <span className={`h-3 w-3 rounded-full ${item.colorClass}`} />
              <span>{item.label}</span>
            </div>
          </CortexTooltip>
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
            Ticket card borders and badges reflect SLA state (On Track, At Risk, Overdue, or
            resolved outcomes).
          </p>
        </div>

        <div className="flex flex-wrap gap-4">
          {legendItems.map((item) => (
            <CortexTooltip key={item.label} content={item.description}>
              <div className="flex cursor-help items-center gap-2 text-sm text-gray-700 dark:text-slate-300">
                <span className={`h-3 w-3 rounded-full ${item.colorClass}`} />
                <span>{item.label}</span>
              </div>
            </CortexTooltip>
          ))}
        </div>
      </div>
    </div>
  );
}
