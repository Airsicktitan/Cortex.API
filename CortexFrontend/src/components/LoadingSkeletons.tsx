import type { ReactNode } from "react";
import { ScrollableViewport } from "./ui/ScrollableViewport";

function SkeletonBlock({ className = "" }: { className?: string }) {
  return (
    <div
      className={`animate-pulse rounded-md bg-gray-200/80 dark:bg-slate-800/80 ${className}`}
    />
  );
}

function SkeletonSection({
  children,
  className = "",
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <section
      className={`rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900 ${className}`}
    >
      {children}
    </section>
  );
}

function HeaderSkeleton({ actionWidth = "w-24" }: { actionWidth?: string }) {
  return (
    <SkeletonSection>
      <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-3">
          <SkeletonBlock className="h-7 w-40" />
          <SkeletonBlock className="h-4 w-80 max-w-full" />
        </div>
        {actionWidth ? <SkeletonBlock className={`h-10 ${actionWidth}`} /> : null}
      </div>
    </SkeletonSection>
  );
}

function SummaryCardSkeleton() {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
      <SkeletonBlock className="h-4 w-24" />
      <SkeletonBlock className="mt-4 h-8 w-16" />
      <SkeletonBlock className="mt-3 h-4 w-36" />
    </div>
  );
}

function TableSkeleton({
  columns,
  rows,
}: {
  columns: number;
  rows: number;
}) {
  return (
    <div className="overflow-hidden rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
        <SkeletonBlock className="h-6 w-40" />
        <SkeletonBlock className="mt-3 h-4 w-72 max-w-full" />
      </div>
      <div className="px-6 py-5">
        <div className="space-y-4">
          <div className="grid gap-4" style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }}>
            {Array.from({ length: columns }).map((_, index) => (
              <SkeletonBlock key={`header-${index}`} className="h-4 w-20" />
            ))}
          </div>
          {Array.from({ length: rows }).map((_, rowIndex) => (
            <div
              key={`row-${rowIndex}`}
              className="grid gap-4 border-t border-gray-100 pt-4 dark:border-slate-800"
              style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }}
            >
              {Array.from({ length: columns }).map((__, columnIndex) => (
                <SkeletonBlock
                  key={`cell-${rowIndex}-${columnIndex}`}
                  className={`h-4 ${columnIndex === 0 ? "w-24" : "w-full"}`}
                />
              ))}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

export function DashboardSkeleton() {
  return (
    <div className="flex min-h-0 flex-col gap-6 lg:h-full lg:overflow-hidden">
      <HeaderSkeleton />

      <ScrollableViewport
        outerClassName="lg:min-h-0 lg:flex-1"
        viewportClassName="scroll-chain-auto space-y-6 lg:h-full lg:min-h-0 lg:overflow-y-auto lg:pr-1"
        affordanceAriaLabel="Scroll dashboard loading state to bottom"
      >
        <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
          {Array.from({ length: 5 }).map((_, index) => (
            <SummaryCardSkeleton key={index} />
          ))}
        </section>

        <div className="grid gap-6 xl:grid-cols-3">
          {Array.from({ length: 3 }).map((_, index) => (
            <TableSkeleton key={index} columns={2} rows={4} />
          ))}
        </div>

        <div className="grid gap-6 xl:grid-cols-2">
          {Array.from({ length: 2 }).map((_, index) => (
            <TableSkeleton key={index} columns={5} rows={5} />
          ))}
        </div>
      </ScrollableViewport>
    </div>
  );
}

export function ReportsSkeleton() {
  return (
    <div className="space-y-6">
      <HeaderSkeleton actionWidth="w-32" />

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }).map((_, index) => (
          <SummaryCardSkeleton key={index} />
        ))}
      </section>

      <TableSkeleton columns={4} rows={5} />

      <div className="grid gap-6 xl:grid-cols-2">
        {Array.from({ length: 2 }).map((_, index) => (
          <TableSkeleton key={index} columns={5} rows={5} />
        ))}
      </div>
    </div>
  );
}

export function TicketGridSkeleton() {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-[repeat(auto-fit,minmax(min(100%,20rem),1fr))] gap-4">
        {Array.from({ length: 6 }).map((_, index) => (
          <div
            key={index}
            className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900"
          >
            <div className="space-y-4">
              <div className="flex items-start justify-between gap-3">
                <div className="space-y-2">
                  <SkeletonBlock className="h-5 w-24" />
                  <SkeletonBlock className="h-4 w-48 max-w-full" />
                </div>
                <SkeletonBlock className="h-6 w-20 rounded-full" />
              </div>

              <SkeletonBlock className="h-4 w-full" />
              <SkeletonBlock className="h-4 w-5/6" />
              <SkeletonBlock className="h-4 w-4/6" />

              <div className="grid grid-cols-2 gap-3 pt-2">
                <SkeletonBlock className="h-10 w-full" />
                <SkeletonBlock className="h-10 w-full" />
              </div>
            </div>
          </div>
        ))}
      </div>

      <div className="flex flex-col gap-3 rounded-lg border border-gray-200 bg-white/80 px-4 py-3 dark:border-slate-800 dark:bg-slate-900/80 sm:flex-row sm:items-center sm:justify-between">
        <SkeletonBlock className="h-4 w-44" />
        <div className="flex gap-3">
          <SkeletonBlock className="h-10 w-24" />
          <SkeletonBlock className="h-10 w-24" />
        </div>
      </div>
    </div>
  );
}

export function ArchivedTicketsSkeleton() {
  return (
    <div className="space-y-6">
      <HeaderSkeleton />
      <TableSkeleton columns={8} rows={6} />
    </div>
  );
}

export function UsersSkeleton() {
  return (
    <div className="space-y-6">
      <HeaderSkeleton />
      <TableSkeleton columns={8} rows={6} />
    </div>
  );
}

export function ConfigurationSkeleton() {
  return (
    <div className="space-y-6">
      <HeaderSkeleton actionWidth="" />

      {Array.from({ length: 4 }).map((_, index) => (
        <div
          key={index}
          className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900"
        >
          <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
              <div className="space-y-3">
                <SkeletonBlock className="h-6 w-44" />
                <SkeletonBlock className="h-4 w-80 max-w-full" />
              </div>
              <div className="flex gap-3">
                <SkeletonBlock className="h-10 w-24" />
                <SkeletonBlock className="h-10 w-32" />
              </div>
            </div>
          </div>

          <div className="space-y-4 px-6 py-6">
            {Array.from({ length: 4 }).map((__, rowIndex) => (
              <div
                key={rowIndex}
                className="grid gap-4 md:grid-cols-[1.2fr_1fr_1fr]"
              >
                <div className="space-y-2">
                  <SkeletonBlock className="h-4 w-28" />
                  <SkeletonBlock className="h-4 w-full" />
                </div>
                <SkeletonBlock className="h-11 w-full" />
                <SkeletonBlock className="h-11 w-full" />
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
