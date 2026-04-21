import {
  useCallback,
  useEffect,
  useState,
  type RefObject,
} from "react";

const DEFAULT_BOTTOM_THRESHOLD_PX = 20;

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
    <button
      type="button"
      onClick={() => {
        const el = containerRef.current;
        if (el) {
          scrollElementToBottom(el, "smooth");
        }
      }}
      className={`absolute bottom-3 right-3 z-10 flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-gray-300/80 bg-white/85 text-gray-700 shadow-md backdrop-blur-sm transition hover:bg-white hover:text-gray-900 hover:shadow-lg focus:outline-none focus-visible:ring-2 focus-visible:ring-cortex-blue focus-visible:ring-offset-2 dark:border-slate-600 dark:bg-slate-900/85 dark:text-slate-200 dark:hover:bg-slate-800 dark:hover:text-white dark:focus-visible:ring-offset-slate-900 ${className}`}
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
  );
}
