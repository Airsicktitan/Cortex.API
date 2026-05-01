import {
  useRef,
  type ComponentPropsWithoutRef,
  type ReactNode,
  type RefObject,
} from "react";
import { ScrollToBottomButton } from "./ScrollToBottomButton";

type ScrollableViewportProps = {
  children: ReactNode;
  viewportRef?: RefObject<HTMLDivElement | null>;
  outerClassName?: string;
  viewportClassName?: string;
  affordanceClassName?: string;
  affordanceAriaLabel?: string;
  /** Pixel step for the floating scroll control; omit to jump to bottom. */
  affordanceScrollStepPx?: number;
  showAffordance?: boolean;
  viewportProps?: Omit<
    ComponentPropsWithoutRef<"div">,
    "children" | "className" | "ref"
  >;
};

export function ScrollableViewport({
  children,
  viewportRef,
  outerClassName = "",
  viewportClassName = "",
  affordanceClassName = "",
  affordanceAriaLabel = "Scroll to bottom",
  affordanceScrollStepPx,
  showAffordance = true,
  viewportProps,
}: ScrollableViewportProps) {
  const fallbackViewportRef = useRef<HTMLDivElement | null>(null);
  const resolvedViewportRef = viewportRef ?? fallbackViewportRef;

  return (
    <div className={`relative min-h-0 overflow-hidden ${outerClassName}`.trim()}>
      <div
        ref={resolvedViewportRef}
        className={`scroll-surface min-h-0 overscroll-y-contain ${viewportClassName}`.trim()}
        {...viewportProps}
      >
        {children}
      </div>
      {showAffordance ? (
        <ScrollToBottomButton
          containerRef={resolvedViewportRef}
          scrollStepPx={affordanceScrollStepPx}
          className={affordanceClassName}
          aria-label={affordanceAriaLabel}
        />
      ) : null}
    </div>
  );
}
