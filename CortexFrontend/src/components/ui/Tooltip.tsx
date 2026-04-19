/**
 * Cortex tooltip design system (Radix UI).
 *
 * Conventions — helper text vs tooltip:
 * - **Helper text** (copy next to labels/fields) = primary guidance. Users should understand
 *   the feature without hovering.
 * - **Tooltip** = short supplemental clarification only. Never the sole place critical meaning
 *   is explained.
 * - Do **not** use raw HTML `title` for product UX (native tooltips are delayed, inconsistent,
 *   and poor for accessibility). Use {@link CortexTooltip} or the primitives below.
 *
 * Defaults: open delay 100ms; `skipDelayDuration` 0 (no extra delay when switching triggers);
 * placement top; hover + keyboard focus via Radix; light/dark styling; max width for longer copy.
 */
import * as React from "react";
import * as TooltipPrimitive from "@radix-ui/react-tooltip";

const DEFAULT_DELAY_MS = 100;
const DEFAULT_SKIP_DELAY_MS = 0;

type ProviderProps = React.ComponentProps<typeof TooltipPrimitive.Provider>;

/**
 * App root wrapper. Applies standardized delay; override per prop if needed.
 */
export function TooltipProvider({
  delayDuration = DEFAULT_DELAY_MS,
  skipDelayDuration = DEFAULT_SKIP_DELAY_MS,
  ...props
}: ProviderProps) {
  return (
    <TooltipPrimitive.Provider
      delayDuration={delayDuration}
      skipDelayDuration={skipDelayDuration}
      {...props}
    />
  );
}

/** Low-level primitive for advanced layouts (prefer {@link CortexTooltip}). */
export const Tooltip = TooltipPrimitive.Root;
export const TooltipTrigger = TooltipPrimitive.Trigger;

const contentSurfaceClass =
  "z-[100] max-w-sm rounded-md border border-gray-200 bg-white px-2.5 py-1.5 text-xs leading-snug text-gray-800 shadow-md dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100";

/**
 * Styled tooltip surface — consistent light/dark, readable width for longer helper copy.
 */
export const TooltipContent = React.forwardRef<
  React.ElementRef<typeof TooltipPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof TooltipPrimitive.Content>
>(({ className = "", ...props }, ref) => (
  <TooltipPrimitive.Portal>
    <TooltipPrimitive.Content
      ref={ref}
      sideOffset={6}
      className={`${contentSurfaceClass} ${className}`}
      {...props}
    />
  </TooltipPrimitive.Portal>
));
TooltipContent.displayName = "TooltipContent";

export type CortexTooltipProps = {
  /** Supplementary copy only — keep short; see module doc. */
  content: React.ReactNode;
  /** Single focusable/hoverable child (e.g. button). */
  children: React.ReactElement;
  /** @default "top" */
  side?: React.ComponentProps<typeof TooltipPrimitive.Content>["side"];
  align?: React.ComponentProps<typeof TooltipPrimitive.Content>["align"];
  /** Merged into content surface */
  contentClassName?: string;
};

/**
 * Standard Cortex tooltip: trigger + content with defaults (top placement, shared styling).
 */
export function CortexTooltip({
  content,
  children,
  side = "top",
  align = "center",
  contentClassName = "",
}: CortexTooltipProps) {
  return (
    <TooltipPrimitive.Root delayDuration={DEFAULT_DELAY_MS}>
      <TooltipPrimitive.Trigger asChild>{children}</TooltipPrimitive.Trigger>
      <TooltipContent side={side} align={align} className={contentClassName}>
        {content}
      </TooltipContent>
    </TooltipPrimitive.Root>
  );
}
