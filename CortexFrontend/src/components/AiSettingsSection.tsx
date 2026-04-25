import { useEffect, useMemo, useRef, useState } from "react";
import type { ChangeEvent } from "react";
import type { AiSettingsConfiguration } from "../types/aiSettings";
import { formatDisplayDateTime, formatDisplayValue } from "../utils/presentation";
import {
  ConfigDetailCard,
  ConfigErrorBanner,
  ConfigPageBody,
  ConfigPageHeader,
  ConfigPageShell,
  ConfigPrimaryButton,
  ConfigSecondaryButton,
  configFieldClass,
} from "./configurationAdminUi";
import { CortexTooltip } from "./ui/Tooltip";

interface AiSettingsSectionProps {
  configuration: AiSettingsConfiguration | null;
  loading: boolean;
  saving: boolean;
  error: string | null;
  onChange: <K extends keyof AiSettingsConfiguration>(
    field: K,
    value: AiSettingsConfiguration[K],
  ) => void;
  onRefresh: () => void;
  onSave: () => void;
}

type AiControlMode = "fully-automated" | "assisted" | "advisory-only";

interface TooltipLabelProps {
  htmlFor?: string;
  label: string;
  tooltip: string;
}

interface ToggleFieldProps extends TooltipLabelProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
}

interface InputFieldProps extends TooltipLabelProps {
  value: string | number;
  type?: "text" | "number";
  min?: number;
  max?: number;
  step?: number;
  onChange: (value: string | number) => void;
}

interface SelectFieldProps extends TooltipLabelProps {
  value: string;
  options: string[];
  onChange: (value: string) => void;
}

interface ConfidenceFieldProps extends TooltipLabelProps {
  value: number;
  onChange: (value: number) => void;
}

interface ControlModeOption {
  id: AiControlMode;
  title: string;
  description: string;
  helper?: string;
}

const TEXT_MODEL_OPTIONS = [
  "gpt-4o-mini",
  "gpt-4o",
  "gpt-4.1-mini",
  "gpt-4.1",
] as const;

const VISION_MODEL_OPTIONS = [
  "gpt-4o-mini",
  "gpt-4o",
  "gpt-4.1-mini",
] as const;

const CONTROL_MODE_OPTIONS: readonly ControlModeOption[] = [
  {
    id: "fully-automated",
    title: "Automation Ready",
    description:
      "Cortex recommendations can flow through configured automations, while routing decisions remain rule-based.",
    helper:
      "Individual capability toggles below still apply as the outer limit.",
  },
  {
    id: "assisted",
    title: "Assisted",
    description: "Cortex suggests next steps and humans decide what to apply.",
  },
  {
    id: "advisory-only",
    title: "Advisory Only",
    description: "Cortex surfaces intake context only and does not drive workflow changes.",
  },
] as const;

function normalizeThreshold(value: number) {
  if (!Number.isFinite(value)) {
    return 0;
  }
  return Math.min(1, Math.max(0, value));
}

function configurationHash(configuration: AiSettingsConfiguration): string {
  const normalized = {
    ...configuration,
    confidenceThreshold: Number(
      normalizeThreshold(configuration.confidenceThreshold).toFixed(4),
    ),
    temperature: Number((Number.isFinite(configuration.temperature) ? configuration.temperature : 0).toFixed(4)),
  };
  return JSON.stringify(normalized);
}

function mergeModelOptions(current: string, defaults: readonly string[]) {
  const items = [...defaults];
  if (!items.includes(current)) {
    return [current, ...items];
  }
  return items;
}

function deriveControlMode(configuration: AiSettingsConfiguration): AiControlMode {
  if (configuration.advisoryOnlyMode) {
    return "advisory-only";
  }
  if (configuration.suggestionOnlyMode) {
    return "assisted";
  }
  return "fully-automated";
}

function TooltipLabel({ htmlFor, label, tooltip }: TooltipLabelProps) {
  return (
    <div className="mb-1 flex items-center gap-2">
      <label
        htmlFor={htmlFor}
        className="text-sm font-medium text-gray-700 dark:text-slate-300"
      >
        {label}
      </label>
      <CortexTooltip content={tooltip}>
        <button
          type="button"
          className="inline-flex h-5 w-5 items-center justify-center rounded-full border border-gray-300 text-[11px] font-semibold text-gray-500 transition hover:border-gray-400 hover:text-gray-700 focus:outline-none focus:ring-2 focus:ring-cortex-blue/30 dark:border-slate-600 dark:text-slate-400 dark:hover:border-slate-500 dark:hover:text-slate-200"
          aria-label={`${label} help`}
        >
          ?
        </button>
      </CortexTooltip>
    </div>
  );
}

function ToggleField({
  htmlFor,
  label,
  tooltip,
  checked,
  onChange,
}: ToggleFieldProps) {
  return (
    <div
      className="flex items-start justify-between gap-4 rounded-lg border border-gray-200 bg-white px-4 py-3 transition hover:border-gray-300 dark:border-slate-600 dark:bg-slate-900 dark:hover:border-slate-500"
    >
      <div className="min-w-0">
        <TooltipLabel htmlFor={htmlFor} label={label} tooltip={tooltip} />
      </div>
      <input
        id={htmlFor}
        type="checkbox"
        className="mt-0.5 h-4 w-4 rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
      />
    </div>
  );
}

function InputField({
  htmlFor,
  label,
  tooltip,
  value,
  type = "text",
  min,
  max,
  step,
  onChange,
}: InputFieldProps) {
  const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
    if (type === "number") {
      onChange(event.target.value === "" ? 0 : Number(event.target.value));
      return;
    }

    onChange(event.target.value);
  };

  return (
    <div>
      <TooltipLabel htmlFor={htmlFor} label={label} tooltip={tooltip} />
      <input
        id={htmlFor}
        type={type}
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={handleChange}
        className={configFieldClass}
      />
    </div>
  );
}

function SelectField({
  htmlFor,
  label,
  tooltip,
  value,
  options,
  onChange,
}: SelectFieldProps) {
  return (
    <div>
      <TooltipLabel htmlFor={htmlFor} label={label} tooltip={tooltip} />
      <select
        id={htmlFor}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className={configFieldClass}
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </div>
  );
}

function ConfidenceThresholdField({
  htmlFor,
  label,
  tooltip,
  value,
  onChange,
}: ConfidenceFieldProps) {
  const safeValue = normalizeThreshold(value);
  const percentageLabel = `${Math.round(safeValue * 100)}%`;

  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 dark:border-slate-600 dark:bg-slate-900">
      <div className="mb-2 flex items-center justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <label
              htmlFor={htmlFor}
              className="text-sm font-medium text-gray-700 dark:text-slate-300"
            >
              {label}
            </label>
            <CortexTooltip content={tooltip}>
              <button
                type="button"
                className="inline-flex h-5 w-5 items-center justify-center rounded-full border border-gray-300 text-[11px] font-semibold text-gray-500 transition hover:border-gray-400 hover:text-gray-700 focus:outline-none focus:ring-2 focus:ring-cortex-blue/30 dark:border-slate-600 dark:text-slate-400 dark:hover:border-slate-500 dark:hover:text-slate-200"
                aria-label={`${label} help`}
              >
                ?
              </button>
            </CortexTooltip>
          </div>
        </div>
        <span className="shrink-0 text-sm font-semibold tabular-nums text-gray-900 dark:text-slate-100">
          {percentageLabel}
        </span>
      </div>
      <div className="space-y-3">
        <div className="flex items-center gap-3">
          <input
            id={htmlFor}
            type="range"
            min={0}
            max={1}
            step={0.01}
            value={safeValue}
            onChange={(event) => onChange(Number(event.target.value))}
            className="h-2 w-full cursor-pointer accent-cortex-blue"
          />
        </div>
        <div className="flex items-center gap-2">
          <input
            type="number"
            min={0}
            max={1}
            step={0.01}
            value={safeValue}
            onChange={(event) =>
              onChange(normalizeThreshold(Number(event.target.value)))
            }
            className={`${configFieldClass} max-w-[7rem]`}
          />
          <p className="text-xs text-gray-500 dark:text-slate-500">
            Minimum confidence required before AI recommendations should be considered reliable.
          </p>
        </div>
      </div>
    </div>
  );
}

function applyControlMode(
  mode: AiControlMode,
  onChange: AiSettingsSectionProps["onChange"],
) {
  const updates: Pick<
    AiSettingsConfiguration,
    "advisoryOnlyMode" | "suggestionOnlyMode"
  > =
    mode === "advisory-only"
      ? {
          advisoryOnlyMode: true,
          suggestionOnlyMode: true,
        }
      : mode === "assisted"
        ? {
            advisoryOnlyMode: false,
            suggestionOnlyMode: true,
          }
        : {
            advisoryOnlyMode: false,
            suggestionOnlyMode: false,
          };

  (Object.entries(updates) as [
    keyof typeof updates,
    (typeof updates)[keyof typeof updates],
  ][]).forEach(([field, value]) => {
    onChange(field, value);
  });
}

export default function AiSettingsSection({
  configuration,
  loading,
  saving,
  error,
  onChange,
  onRefresh,
  onSave,
}: AiSettingsSectionProps) {
  const [baselineHash, setBaselineHash] = useState<string | null>(null);
  const wasLoadingRef = useRef(loading);
  const wasSavingRef = useRef(saving);

  const currentHash = useMemo(
    () => (configuration ? configurationHash(configuration) : null),
    [configuration],
  );

  useEffect(() => {
    const justLoaded = wasLoadingRef.current && !loading;
    const saveFinished = wasSavingRef.current && !saving;

    if (
      configuration &&
      (baselineHash === null ||
        justLoaded ||
        (saveFinished && !error))
    ) {
      // Baseline tracks the last loaded/saved server state for the sticky save bar.
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setBaselineHash(configurationHash(configuration));
    }

    wasLoadingRef.current = loading;
    wasSavingRef.current = saving;
  }, [configuration, loading, saving, error, baselineHash]);

  const isDirty =
    !!configuration &&
    !!currentHash &&
    !!baselineHash &&
    currentHash !== baselineHash;

  const controlMode = configuration ? deriveControlMode(configuration) : "assisted";

  const textModelOptions = useMemo(
    () =>
      configuration
        ? mergeModelOptions(configuration.defaultTextModel, TEXT_MODEL_OPTIONS)
        : [...TEXT_MODEL_OPTIONS],
    [configuration],
  );
  const visionModelOptions = useMemo(
    () =>
      configuration
        ? mergeModelOptions(configuration.defaultVisionModel, VISION_MODEL_OPTIONS)
        : [...VISION_MODEL_OPTIONS],
    [configuration],
  );

  const activeCapabilities = configuration
    ? [
        configuration.isIntakeAssistEnabled,
        configuration.isTriageEnabled,
        configuration.isScreenshotInsightEnabled,
        configuration.isSuggestedUpdatesEnabled,
        configuration.isPriorityRecommendationEnabled,
        configuration.isStatusRecommendationEnabled,
      ].filter(Boolean).length
    : 0;
  const totalCapabilities = configuration
    ? [
        configuration.isIntakeAssistEnabled,
        configuration.isTriageEnabled,
        configuration.isScreenshotInsightEnabled,
        configuration.isSuggestedUpdatesEnabled,
        configuration.isPriorityRecommendationEnabled,
        configuration.isStatusRecommendationEnabled,
      ].length
    : 0;

  const decisionInfluence = configuration
    ? configuration.allowPriorityRecommendation &&
      configuration.allowStatusRecommendation
      ? "Status + Priority"
      : configuration.allowPriorityRecommendation
        ? "Priority only"
        : configuration.allowStatusRecommendation
          ? "Status only"
          : "None"
    : "None";

  const summaryBullets = configuration
    ? [
        configuration.isIntakeAssistEnabled || configuration.isTriageEnabled
          ? "Generate intake recommendations: summary, priority, risk, missing information, and category"
          : null,
        configuration.isScreenshotInsightEnabled
          ? "Include screenshot insight as supporting evidence"
          : null,
        configuration.advisoryOnlyMode
          ? "Keep recommendations advisory with rule-based decision logic"
          : configuration.isSuggestedUpdatesEnabled ||
              configuration.allowPriorityRecommendation ||
              configuration.allowStatusRecommendation
            ? "Use recommendation signals as supporting inputs only"
            : null,
        configuration.suggestionOnlyMode || configuration.advisoryOnlyMode
          ? "Require human confirmation before any workflow change"
          : null,
      ].filter((item): item is string => !!item)
    : [];

  const lastChangedMeta = configuration?.lastModifiedDateUtc
    ? `Last updated ${formatDisplayDateTime(configuration.lastModifiedDateUtc)} by ${formatDisplayValue(configuration.lastModifiedByDisplayName)}`
    : "Defaults are active until an admin saves an override.";

  return (
    <ConfigPageShell>
      <ConfigPageHeader
        title="Cortex Assist Settings"
        description="Admin-only controls for intake guidance, visual evidence, recommendation signals, and safety guardrails."
        meta={
          <p className="text-xs text-gray-500 dark:text-slate-400">
            {lastChangedMeta}
          </p>
        }
        actions={
          <>
            <ConfigSecondaryButton onClick={onRefresh} disabled={loading || saving}>
              Refresh
            </ConfigSecondaryButton>
            <ConfigPrimaryButton
              onClick={onSave}
              disabled={!configuration || loading || saving}
            >
              {saving ? "Saving…" : "Save Cortex Assist Settings"}
            </ConfigPrimaryButton>
          </>
        }
      />

      {error ? <ConfigErrorBanner>{error}</ConfigErrorBanner> : null}

      <ConfigPageBody>
        {loading || !configuration ? (
          <div className="rounded-xl border border-dashed border-gray-200 bg-gray-50/80 px-6 py-12 text-center text-sm text-gray-500 dark:border-slate-700 dark:bg-slate-800/30 dark:text-slate-400">
            Loading Cortex Assist settings…
          </div>
        ) : (
          <div className="space-y-6">
            <ConfigDetailCard
              title="Recommendation Control"
              subtitle="Choose how Cortex recommendations appear in production."
            >
              <div className="space-y-2.5">
                {CONTROL_MODE_OPTIONS.map((mode) => {
                  const selected = controlMode === mode.id;
                  return (
                    <label
                      key={mode.id}
                      className={`flex cursor-pointer items-start gap-3 rounded-lg border px-4 py-3 transition ${
                        selected
                          ? "border-cortex-blue bg-cortex-blue/5 ring-1 ring-cortex-blue/20 dark:border-cortex-blue dark:bg-cortex-blue/10 dark:ring-cortex-blue/30"
                          : "border-gray-200 bg-white hover:border-gray-300 dark:border-slate-600 dark:bg-slate-900 dark:hover:border-slate-500"
                      }`}
                    >
                      <input
                        type="radio"
                        name="ai-control-mode"
                        className="mt-1 h-4 w-4 border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                        checked={selected}
                        onChange={() => applyControlMode(mode.id, onChange)}
                      />
                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <p className="text-sm font-semibold text-gray-900 dark:text-slate-100">
                            {mode.title}
                          </p>
                        </div>
                        <p className="mt-0.5 text-xs text-gray-600 dark:text-slate-400">
                          {mode.description}
                        </p>
                        {mode.helper ? (
                          <p className="mt-0.5 text-[11px] text-gray-500 dark:text-slate-500">
                            {mode.helper}
                          </p>
                        ) : null}
                      </div>
                    </label>
                  );
                })}
              </div>
            </ConfigDetailCard>

            <ConfigDetailCard
              title="Safety / Constraints"
              subtitle="Safety posture for Cortex recommendations."
            >
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 dark:border-slate-600 dark:bg-slate-900">
                  <p className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
                    Mode
                  </p>
                  <p className="mt-1 text-sm font-semibold text-gray-900 dark:text-slate-100">
                    {controlMode === "advisory-only"
                      ? "Advisory Only"
                      : controlMode === "assisted"
                        ? "Assisted"
                        : "Automation Ready"}
                  </p>
                </div>
                <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 dark:border-slate-600 dark:bg-slate-900">
                  <p className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
                    Active Capabilities
                  </p>
                  <p className="mt-1 text-sm font-semibold text-gray-900 dark:text-slate-100">
                    {activeCapabilities} of {totalCapabilities} capabilities
                    {" "}enabled
                  </p>
                </div>
                <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 dark:border-slate-600 dark:bg-slate-900">
                  <p className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
                    Decision Signals
                  </p>
                  <p className="mt-1 text-sm font-semibold text-gray-900 dark:text-slate-100">
                    {decisionInfluence}
                  </p>
                </div>
                <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 dark:border-slate-600 dark:bg-slate-900">
                  <p className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
                    Confidence Threshold
                  </p>
                  <p className="mt-1 text-sm font-semibold text-gray-900 dark:text-slate-100">
                    {Math.round(normalizeThreshold(configuration.confidenceThreshold) * 100)}%
                  </p>
                </div>
              </div>

              {summaryBullets.length > 0 ? (
                <div className="mt-4 rounded-lg border border-gray-200 bg-white px-4 py-3 dark:border-slate-600 dark:bg-slate-900">
                  <p className="text-xs font-semibold text-gray-700 dark:text-slate-300">
                    Recommendation behavior:
                  </p>
                  <ul className="mt-1 list-disc space-y-0.5 pl-5 text-xs text-gray-600 dark:text-slate-400">
                    {summaryBullets.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                </div>
              ) : null}
            </ConfigDetailCard>

            {controlMode === "advisory-only" ? (
              <div className="rounded-xl border border-sky-200 bg-sky-50/70 px-4 py-3 text-sm text-sky-900 dark:border-sky-800/60 dark:bg-sky-950/30 dark:text-sky-200">
                <p className="font-semibold">Cortex is in Advisory Mode</p>
                <p className="mt-0.5 text-xs">
                  Cortex provides recommendations and evidence only. Decision and assignment logic remains rule-based.
                </p>
              </div>
            ) : null}

            <ConfigDetailCard
              title="Intake Guidance + Visual Evidence"
              subtitle="Intake recommendation scope and how vision contributes evidence."
            >
              <div className="grid gap-3 lg:grid-cols-2">
                <ToggleField
                  htmlFor="ai-intake-assist"
                  label="Intake Assist enabled"
                  tooltip="On — Cortex suggests structured ticket fields as users type. Off — intake is fully manual."
                  checked={configuration.isIntakeAssistEnabled}
                  onChange={(value) => onChange("isIntakeAssistEnabled", value)}
                />
                <ToggleField
                  htmlFor="ai-triage"
                  label="Intake Assessment enabled"
                  tooltip="On — Cortex generates intake recommendations: summary, priority, risk, missing information, and category."
                  checked={configuration.isTriageEnabled}
                  onChange={(value) => onChange("isTriageEnabled", value)}
                />
                <ToggleField
                  htmlFor="ai-screenshot-insight"
                  label="Vision Evidence enabled"
                  tooltip="On — screenshot insight contributes evidence to the unified assessment."
                  checked={configuration.isScreenshotInsightEnabled}
                  onChange={(value) =>
                    onChange("isScreenshotInsightEnabled", value)
                  }
                />
                <ToggleField
                  htmlFor="ai-suggested-updates"
                  label="Suggested Updates enabled"
                  tooltip="Cortex proposes edits to ticket description and fields for the owner to accept or reject."
                  checked={configuration.isSuggestedUpdatesEnabled}
                  onChange={(value) =>
                    onChange("isSuggestedUpdatesEnabled", value)
                  }
                />
                <ToggleField
                  htmlFor="ai-priority-recommendation"
                  label="Suggested Priority enabled"
                  tooltip="Cortex recommends a priority level based on ticket content and impact signals."
                  checked={configuration.isPriorityRecommendationEnabled}
                  onChange={(value) =>
                    onChange("isPriorityRecommendationEnabled", value)
                  }
                />
                <ToggleField
                  htmlFor="ai-status-recommendation"
                  label="Suggested Status enabled"
                  tooltip="Cortex recommends the next status transition based on ticket activity and state."
                  checked={configuration.isStatusRecommendationEnabled}
                  onChange={(value) =>
                    onChange("isStatusRecommendationEnabled", value)
                  }
                />
              </div>
            </ConfigDetailCard>

            <ConfigDetailCard
              title="Decision Signals"
              subtitle="How recommendation signals can support rule-based decisions."
            >
              <div className="grid gap-3 lg:grid-cols-2">
                <ToggleField
                  htmlFor="ai-allow-status-recommendation"
                  label="Allow Status Recommendation"
                  tooltip="On — suggested status can appear as an advisory decision signal."
                  checked={configuration.allowStatusRecommendation}
                  onChange={(value) =>
                    onChange("allowStatusRecommendation", value)
                  }
                />
                <ToggleField
                  htmlFor="ai-allow-priority-recommendation"
                  label="Allow Priority Recommendation"
                  tooltip="On — suggested priority can appear as an advisory decision signal."
                  checked={configuration.allowPriorityRecommendation}
                  onChange={(value) =>
                    onChange("allowPriorityRecommendation", value)
                  }
                />
                <ToggleField
                  htmlFor="ai-suggestion-only-mode"
                  label="Suggestion-only Mode"
                  tooltip="On — recommendations require human confirmation before any workflow change."
                  checked={configuration.suggestionOnlyMode}
                  onChange={(value) => onChange("suggestionOnlyMode", value)}
                />
                <InputField
                  htmlFor="ai-max-screenshot-attachment-count"
                  label="Screenshot Limit"
                  tooltip="Cap on images sent per screenshot insight request. Lower values reduce cost and latency."
                  type="number"
                  min={1}
                  max={8}
                  step={1}
                  value={configuration.maxScreenshotAttachmentCount}
                  onChange={(value) =>
                    onChange("maxScreenshotAttachmentCount", Number(value))
                  }
                />
              </div>
              <div className="mt-3">
                <ConfidenceThresholdField
                  htmlFor="ai-confidence-threshold"
                  label="Confidence Threshold"
                  tooltip="Low-confidence recommendations are de-emphasized before they can be used."
                  value={configuration.confidenceThreshold}
                  onChange={(value) => onChange("confidenceThreshold", value)}
                />
              </div>
            </ConfigDetailCard>

            <div className="border-t border-gray-200/80 pt-5 dark:border-slate-700/70">
              <ConfigDetailCard
                title="Service Settings"
                subtitle="Operational limits for Cortex Assist requests."
              >
                <div className="grid gap-4 lg:grid-cols-2">
                <SelectField
                  htmlFor="ai-default-text-model"
                  label="Text Analysis Model"
                  tooltip="Model used for unified intake assessment and text recommendations."
                  value={configuration.defaultTextModel}
                  options={textModelOptions}
                  onChange={(value) => onChange("defaultTextModel", value)}
                />
                <SelectField
                  htmlFor="ai-default-vision-model"
                  label="Visual Evidence Model"
                  tooltip="Model used for screenshot insight evidence extraction."
                  value={configuration.defaultVisionModel}
                  options={visionModelOptions}
                  onChange={(value) => onChange("defaultVisionModel", value)}
                />
                <InputField
                  htmlFor="ai-temperature"
                  label="Response Variety"
                  tooltip="Controls how varied generated recommendations can be. Lower values are more consistent."
                  type="number"
                  min={0}
                  max={2}
                  step={0.1}
                  value={configuration.temperature}
                  onChange={(value) => onChange("temperature", Number(value))}
                />
                <InputField
                  htmlFor="ai-max-tokens"
                  label="Response Length Limit"
                  tooltip="Caps the length of each generated recommendation."
                  type="number"
                  min={1}
                  max={4000}
                  step={1}
                  value={configuration.maxTokens}
                  onChange={(value) => onChange("maxTokens", Number(value))}
                />
                <InputField
                  htmlFor="ai-timeout-seconds"
                  label="Response Time Limit"
                  tooltip="Cancel a request if it does not complete within this many seconds."
                  type="number"
                  min={5}
                  max={300}
                  step={1}
                  value={configuration.timeoutSeconds}
                  onChange={(value) =>
                    onChange("timeoutSeconds", Number(value))
                  }
                />
                <InputField
                  htmlFor="ai-retry-count"
                  label="Retry Limit"
                  tooltip="Number of times Cortex retries a failed request before giving up."
                  type="number"
                  min={0}
                  max={3}
                  step={1}
                  value={configuration.retryCount}
                  onChange={(value) => onChange("retryCount", Number(value))}
                />
                </div>
              </ConfigDetailCard>
            </div>
          </div>
        )}
      </ConfigPageBody>

      {isDirty && configuration ? (
        <div className="sticky bottom-0 z-20 border-t border-amber-200 bg-amber-50/95 px-6 py-3 backdrop-blur dark:border-amber-900/50 dark:bg-slate-900/95">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm font-medium text-amber-900 dark:text-amber-200">
              Unsaved changes
            </p>
            <ConfigPrimaryButton
              onClick={onSave}
              disabled={loading || saving}
              className="min-w-[10rem]"
            >
              {saving ? "Saving…" : "Save Cortex Assist Settings"}
            </ConfigPrimaryButton>
          </div>
        </div>
      ) : null}
    </ConfigPageShell>
  );
}
