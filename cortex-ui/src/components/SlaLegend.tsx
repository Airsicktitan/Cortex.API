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

export default function SlaLegend() {
  return (
    <div className="bg-white dark:bg-slate-900 rounded-lg border border-gray-200 dark:border-slate-800 p-4 mb-6">
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
