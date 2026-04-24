import { memo, useEffect, useRef, useState, type MouseEvent as ReactMouseEvent } from "react";
import { ScrollableViewport } from "./ui/ScrollableViewport";

type SidebarView =
  | "dashboard"
  | "tickets"
  | "approval"
  | "archived"
  | "reports"
  | "rebalance"
  | "sla"
  | "jobs"
  | "users";

type SidebarNavigationItem = {
  view: SidebarView;
  group: "workspace" | "admin";
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

function AppSidebar({
  width,
  activeView,
  navigationItems,
  onViewChange,
  onResize,
}: AppSidebarProps) {
  const workspaceItems = navigationItems.filter((item) => item.group === "workspace");
  const adminItems = navigationItems.filter((item) => item.group === "admin");

  const [isResizing, setIsResizing] = useState(false);
  const navScrollRef = useRef<HTMLDivElement | null>(null);
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
    <aside className="relative hidden h-full shrink-0 min-h-0 lg:block" style={{ width: `${width}px` }}>
      <div className="flex h-full flex-col overflow-hidden rounded-2xl border border-gray-200 bg-white/90 shadow-sm backdrop-blur dark:border-slate-800 dark:bg-slate-950/85">
        <ScrollableViewport
          viewportRef={navScrollRef}
          outerClassName="flex-1"
          viewportClassName="scroll-chain-auto h-full overflow-y-auto"
          affordanceAriaLabel="Scroll navigation to bottom"
        >
          <nav className="px-4 py-4 pb-14">
              <div className="space-y-8">
                <section className="space-y-3">
                  <p className="px-4 text-xs font-semibold uppercase tracking-[0.16em] text-gray-500 dark:text-slate-400">
                    Workspace
                  </p>
                  <div className="space-y-2">
                    {workspaceItems.map((item) => {
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
                </section>

                {adminItems.length > 0 && (
                  <section className="space-y-3">
                    <p className="px-4 text-xs font-semibold uppercase tracking-[0.16em] text-gray-500 dark:text-slate-400">
                      Admin
                    </p>
                    <div className="space-y-2">
                      {adminItems.map((item) => {
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
                  </section>
                )}
              </div>
          </nav>
        </ScrollableViewport>

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

function areNavigationItemsEqual(
  previousItems: SidebarNavigationItem[],
  nextItems: SidebarNavigationItem[],
) {
  if (previousItems === nextItems) {
    return true;
  }

  if (previousItems.length !== nextItems.length) {
    return false;
  }

  return previousItems.every((item, index) => {
    const nextItem = nextItems[index];
    return (
      item.view === nextItem.view &&
      item.group === nextItem.group &&
      item.label === nextItem.label &&
      item.description === nextItem.description
    );
  });
}

function areAppSidebarPropsEqual(
  previousProps: AppSidebarProps,
  nextProps: AppSidebarProps,
) {
  return (
    previousProps.width === nextProps.width &&
    previousProps.activeView === nextProps.activeView &&
    previousProps.onViewChange === nextProps.onViewChange &&
    previousProps.onResize === nextProps.onResize &&
    areNavigationItemsEqual(
      previousProps.navigationItems,
      nextProps.navigationItems,
    )
  );
}

export default memo(AppSidebar, areAppSidebarPropsEqual);
