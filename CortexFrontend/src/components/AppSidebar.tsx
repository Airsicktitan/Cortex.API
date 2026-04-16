import { useEffect, useRef, useState, type MouseEvent as ReactMouseEvent } from "react";

type SidebarView =
  | "dashboard"
  | "tickets"
  | "archived"
  | "reports"
  | "sla"
  | "jobs"
  | "users";

type SidebarNavigationItem = {
  view: SidebarView;
  label: string;
  description: string;
};

type AppSidebarProps = {
  width: number;
  activeView: SidebarView;
  navigationItems: SidebarNavigationItem[];
  onViewChange: (view: SidebarView) => void;
  onResize: (nextWidth: number) => void;
};

export default function AppSidebar({
  width,
  activeView,
  navigationItems,
  onViewChange,
  onResize,
}: AppSidebarProps) {
  const [isResizing, setIsResizing] = useState(false);
  const resizeStartXRef = useRef(0);
  const resizeStartWidthRef = useRef(width);

  useEffect(() => {
    resizeStartWidthRef.current = width;
  }, [width]);

  useEffect(() => {
    if (!isResizing) {
      return;
    }

    const handlePointerMove = (event: MouseEvent) => {
      const nextWidth =
        resizeStartWidthRef.current + (event.clientX - resizeStartXRef.current);

      onResize(nextWidth);
    };

    const handlePointerUp = () => {
      setIsResizing(false);
    };

    document.body.style.cursor = "col-resize";
    document.body.style.userSelect = "none";

    window.addEventListener("mousemove", handlePointerMove);
    window.addEventListener("mouseup", handlePointerUp);

    return () => {
      document.body.style.cursor = "";
      document.body.style.userSelect = "";
      window.removeEventListener("mousemove", handlePointerMove);
      window.removeEventListener("mouseup", handlePointerUp);
    };
  }, [isResizing, onResize]);

  const beginResize = (event: ReactMouseEvent<HTMLButtonElement>) => {
    resizeStartXRef.current = event.clientX;
    resizeStartWidthRef.current = width;
    setIsResizing(true);
  };

  return (
    <aside className="relative hidden shrink-0 lg:block" style={{ width: `${width}px` }}>
      <div className="sticky top-8 flex h-[calc(100vh-8rem)] flex-col rounded-2xl border border-gray-200 bg-white/90 shadow-sm backdrop-blur dark:border-slate-800 dark:bg-slate-950/85">
        <div className="border-b border-gray-100 px-5 py-5 dark:border-slate-800">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-gray-500 dark:text-slate-400">
            Navigation
          </p>
          <h3 className="mt-2 text-lg font-semibold text-gray-900 dark:text-slate-100">
            Workspace
          </h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            Move between ticket operations, reporting, and admin controls.
          </p>
        </div>

        <nav className="flex-1 overflow-y-auto px-3 py-4">
          <div className="space-y-2">
            {navigationItems.map((item) => {
              const isActive = item.view === activeView;

              return (
                <button
                  key={item.view}
                  onClick={() => onViewChange(item.view)}
                  className={`w-full rounded-xl border px-4 py-3 text-left transition-colors ${
                    isActive
                      ? "border-cortex-blue bg-cortex-blue-soft text-cortex-ink shadow-sm dark:border-cortex-blue dark:bg-cortex-blue/20 dark:text-slate-100"
                      : "border-transparent text-gray-700 hover:border-gray-200 hover:bg-gray-50 dark:text-slate-200 dark:hover:border-slate-700 dark:hover:bg-slate-900"
                  }`}
                >
                  <div className="flex items-center justify-between gap-3">
                    <span className="font-medium">{item.label}</span>
                    {isActive && (
                      <span className="text-xs font-semibold uppercase tracking-wide text-cortex-blue dark:text-cortex-cyan">
                        Active
                      </span>
                    )}
                  </div>
                  <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                    {item.description}
                  </p>
                </button>
              );
            })}
          </div>
        </nav>

        <div className="border-t border-gray-100 px-5 py-4 dark:border-slate-800">
          <p className="text-xs text-gray-500 dark:text-slate-400">
            Drag the right edge to resize the menu.
          </p>
        </div>

        <button
          type="button"
          aria-label="Resize sidebar"
          onMouseDown={beginResize}
          className="absolute inset-y-0 -right-2 hidden w-4 cursor-col-resize items-center justify-center lg:flex"
        >
          <span
            className={`block h-20 w-1 rounded-full transition-colors ${
              isResizing ? "bg-cortex-blue" : "bg-gray-300 dark:bg-slate-700"
            }`}
          />
        </button>
      </div>
    </aside>
  );
}
