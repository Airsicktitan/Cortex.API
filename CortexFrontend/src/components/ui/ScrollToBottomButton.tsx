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
const DEFAULT_BOTTOM_THRESHOLD_PX = 24;

export function scrollElementToBottom(
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
  className = "",
  "aria-label": ariaLabel = "Jump to latest",
}: ScrollToBottomButtonProps) {
  const [visible, setVisible] = useState(false);

  const updateVisibility = useCallback(() => {
    const el = containerRef.current;
    if (!el) {
      setVisible(false);
      return;
    }
    const { scrollTop, clientHeight, scrollHeight } = el;
    const hasOverflow = scrollHeight > clientHeight;
    const distanceFromBottom = scrollHeight - scrollTop - clientHeight;
    const atBottom = distanceFromBottom <= threshold;
    setVisible(hasOverflow && !atBottom);
  }, [containerRef, threshold]);

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

    updateVisibility();
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

  if (!visible) {
    return null;
  }

  return (
    // Wrapper is strictly a positioning slot — `pointer-events-none` so no
    // invisible area around the button can swallow scroll / wheel / hover
    // interaction with the scrollable panel. `w-9 h-9` keeps the wrapper
    // exactly button-sized so the hitbox can never grow larger than the icon.
    <div className="pointer-events-none absolute bottom-3 right-3 z-10 h-9 w-9">
      <button
        type="button"
        onClick={() => {
          const el = containerRef.current;
          if (el) {
            scrollElementToBottom(el, "smooth");
          }
        }}
        // Intentionally NO `backdrop-blur-*`, NO `transition`, NO
        // `hover:shadow-*`: those force the compositor to repaint the button's
        // layer on every scroll frame when the cursor is over it, which
        // manifests as the panel "freezing" near the bottom until the cursor
        // moves off the control. Solid background + static shadow only.
        className={`pointer-events-auto flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-gray-300 bg-white text-gray-700 shadow-md hover:bg-gray-50 hover:text-gray-900 focus:outline-none focus-visible:ring-2 focus-visible:ring-cortex-blue focus-visible:ring-offset-2 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800 dark:hover:text-white dark:focus-visible:ring-offset-slate-900 ${className}`}
        aria-label={ariaLabel}
      >
        <svg
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 24 24"
          fill="none"
          strokeWidth={2}
          stroke="currentColor"
          className="h-5 w-5"
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
