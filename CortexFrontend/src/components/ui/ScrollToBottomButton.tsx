import {
  useCallback,
  useEffect,
  useState,
  type RefObject,
} from "react";

/**
 * Slightly generous bottom threshold: inertial trackpad / touch scrolling can
 * overshoot the exact bottom by a pixel or two, which used to flip `visible`
 * and cause jitter / "freeze" feel right at the bottom edge.
 */
const DEFAULT_BOTTOM_THRESHOLD_PX = 8;
const OVERFLOW_TOLERANCE_PX = 4;
const SCROLLABLE_OVERFLOW_VALUES = new Set(["auto", "scroll", "overlay"]);

function scrollElementToBottom(
  el: HTMLElement,
  behavior: ScrollBehavior = "smooth",
) {
  el.scrollTo({
    top: el.scrollHeight,
    behavior,
  });
}

export type ScrollToBottomButtonProps = {
  containerRef: RefObject<HTMLElement | null>;
  threshold?: number;
  /**
   * When set, each click scrolls down by this many pixels (capped at the bottom).
   * When omitted, click jumps to the bottom of the container.
   */
  scrollStepPx?: number;
  className?: string;
  "aria-label"?: string;
};

/**
 * Floating control for scrollable panels: shows when content overflows and the
 * user is not near the bottom; smooth-scrolls on click.
 *
 * The button is wrapped in a `pointer-events-none` overlay so the surrounding
 * area (and any future visual padding around the control) never intercepts
 * scroll/touch/wheel interaction near the bottom of the scrollable surface —
 * only the button itself re-enables pointer events via `pointer-events-auto`.
 *
 * The scroll listener is purely observational: it updates visibility state
 * only. Programmatic scrolling (`scrollTo`) only happens on explicit click.
 */
export function ScrollToBottomButton({
  containerRef,
  threshold = DEFAULT_BOTTOM_THRESHOLD_PX,
  scrollStepPx,
  className = "",
  "aria-label": ariaLabel = "Jump to latest",
}: ScrollToBottomButtonProps) {
  const [isScrollable, setIsScrollable] = useState(false);
  const [isAtBottom, setIsAtBottom] = useState(true);

  const updateVisibility = useCallback(() => {
    const el = containerRef.current;
    if (!el) {
      setIsScrollable(false);
      setIsAtBottom(true);
      return;
    }

    const style = window.getComputedStyle(el);
    let overflowY = style.overflowY;
    if (
      overflowY === "visible" ||
      overflowY === "clip" ||
      (overflowY === "" && style.overflow)
    ) {
      const o = style.overflow.split(" ")[0];
      if (o) {
        overflowY = o;
      }
    }
    if (!SCROLLABLE_OVERFLOW_VALUES.has(overflowY)) {
      setIsScrollable(false);
      setIsAtBottom(true);
      return;
    }

    const { scrollTop, clientHeight, scrollHeight } = el;
    const hasOverflow = scrollHeight > clientHeight + OVERFLOW_TOLERANCE_PX;
    const distanceFromBottom = scrollHeight - scrollTop - clientHeight;
    const atBottom = distanceFromBottom <= threshold;
    setIsScrollable(hasOverflow);
    setIsAtBottom(!hasOverflow || atBottom);
  }, [containerRef, threshold]);

  const visible = isScrollable && !isAtBottom;

  useEffect(() => {
    const el = containerRef.current;
    if (!el) {
      return;
    }

    let raf = 0;
    const scheduleUpdate = () => {
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(() => updateVisibility());
    };

    scheduleUpdate();
    // Passive + observational: never calls scrollTo / preventDefault here.
    // This listener only computes and sets visibility state.
    el.addEventListener("scroll", scheduleUpdate, { passive: true });

    const resizeObserver = new ResizeObserver(scheduleUpdate);
    resizeObserver.observe(el);

    const mutationObserver = new MutationObserver(scheduleUpdate);
    mutationObserver.observe(el, {
      childList: true,
      subtree: true,
      characterData: true,
    });

    return () => {
      cancelAnimationFrame(raf);
      el.removeEventListener("scroll", scheduleUpdate);
      resizeObserver.disconnect();
      mutationObserver.disconnect();
    };
  }, [containerRef, updateVisibility]);

  return (
    <div
      className={`pointer-events-none absolute bottom-3 left-1/2 z-50 -translate-x-1/2 transition-opacity duration-200 ${
        visible ? "opacity-100" : "opacity-0"
      }`}
      aria-hidden={!visible}
    >
      <button
        type="button"
        tabIndex={visible ? 0 : -1}
        onClick={(e) => {
          e.preventDefault();
          e.stopPropagation();
          const el = containerRef.current;
          if (!el) {
            return;
          }
          if (scrollStepPx != null && scrollStepPx > 0) {
            const maxTop = Math.max(0, el.scrollHeight - el.clientHeight);
            const nextTop = Math.min(el.scrollTop + scrollStepPx, maxTop);
            el.scrollTo({ top: nextTop, behavior: "smooth" });
          } else {
            scrollElementToBottom(el, "smooth");
          }
        }}
        className={`${
          visible ? "pointer-events-auto" : "pointer-events-none"
        } flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-gray-300/80 bg-white/90 text-gray-600 shadow-sm transition-colors hover:bg-white hover:text-gray-900 focus:outline-none focus-visible:ring-2 focus-visible:ring-cortex-blue focus-visible:ring-offset-2 dark:border-slate-600/80 dark:bg-slate-900/90 dark:text-slate-300 dark:hover:bg-slate-800 dark:hover:text-white dark:focus-visible:ring-offset-slate-900 ${className}`}
        aria-label={ariaLabel}
      >
        <svg
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 24 24"
          fill="none"
          strokeWidth={2}
          stroke="currentColor"
          className="h-4 w-4"
          aria-hidden
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="m19.5 8.25-7.5 7.5-7.5-7.5"
          />
        </svg>
      </button>
    </div>
  );
}
