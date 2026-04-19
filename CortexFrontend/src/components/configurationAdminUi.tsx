import type { ButtonHTMLAttributes, ReactNode } from "react";

/** Shared SaaS-style configuration page shell (matches Role Definitions baseline). */
export function ConfigPageShell({ children }: { children: ReactNode }) {
  return (
    <section className="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      {children}
    </section>
  );
}

export function ConfigPageHeader({
  title,
  description,
  meta,
  actions,
}: {
  title: string;
  description: string;
  meta?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0">
          <h2 className="text-xl font-semibold tracking-tight text-gray-900 dark:text-slate-100">{title}</h2>
          <p className="mt-1.5 max-w-2xl text-sm leading-relaxed text-gray-600 dark:text-slate-400">
            {description}
          </p>
          {meta ? <div className="mt-2">{meta}</div> : null}
        </div>
        {actions ? (
          <div className="flex flex-shrink-0 flex-wrap items-center gap-2 lg:justify-end">{actions}</div>
        ) : null}
      </div>
    </div>
  );
}

export function ConfigPageBody({ children }: { children: ReactNode }) {
  return <div className="p-6">{children}</div>;
}

export function ConfigTwoColumn({ left, right }: { left: ReactNode; right: ReactNode }) {
  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(240px,280px)_minmax(0,1fr)]">{left}{right}</div>
  );
}

/** Slightly wider catalog column for labels that need more room (e.g. reports). */
export function ConfigTwoColumnWideCatalog({ left, right }: { left: ReactNode; right: ReactNode }) {
  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(260px,340px)_minmax(0,1fr)]">{left}{right}</div>
  );
}

export function ConfigDetailCard({
  title,
  subtitle,
  children,
  className = "",
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={`rounded-xl border border-gray-200 bg-gray-50/50 p-4 dark:border-slate-700 dark:bg-slate-800/40 ${className}`}
    >
      <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
        {title}
      </h3>
      {subtitle ? (
        <p className="mt-1 text-xs text-gray-500 dark:text-slate-500">{subtitle}</p>
      ) : null}
      <div className="mt-3">{children}</div>
    </div>
  );
}

export function ConfigErrorBanner({ children }: { children: ReactNode }) {
  return (
    <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40">
      <p className="text-sm text-red-700 dark:text-red-300">{children}</p>
    </div>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function configCatalogItemClass(selected: boolean) {
  return selected
    ? "border-cortex-blue bg-blue-50 shadow-sm ring-1 ring-cortex-blue/25 dark:border-cortex-blue dark:bg-slate-800 dark:ring-cortex-blue/30"
    : "border-transparent bg-transparent hover:border-gray-200 hover:bg-gray-50 dark:hover:border-slate-600 dark:hover:bg-slate-800/80";
}

export function ConfigPrimaryButton({
  className = "",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      className={`inline-flex items-center justify-center rounded-lg bg-cortex-blue px-4 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-cortex-blue-dark disabled:opacity-50 dark:shadow-none ${className}`}
      {...props}
    />
  );
}

export function ConfigSecondaryButton({
  className = "",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      className={`inline-flex items-center justify-center rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm font-medium text-gray-700 shadow-sm transition hover:bg-gray-50 disabled:opacity-50 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700 ${className}`}
      {...props}
    />
  );
}

export function ConfigGhostButton({
  className = "",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      className={`inline-flex items-center justify-center rounded-lg px-3 py-2.5 text-sm font-medium text-gray-600 transition hover:bg-gray-100 hover:text-gray-900 disabled:opacity-50 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-slate-200 ${className}`}
      {...props}
    />
  );
}

export const configFieldClass =
  "w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm text-gray-900 shadow-sm placeholder:text-gray-400 focus:border-cortex-blue focus:outline-none focus:ring-2 focus:ring-cortex-blue/20 disabled:opacity-50 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100 dark:placeholder:text-slate-500";
