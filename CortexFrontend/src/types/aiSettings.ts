export interface AiSettingsConfiguration {
  isIntakeAssistEnabled: boolean;
  isTriageEnabled: boolean;
  isScreenshotInsightEnabled: boolean;
  isSuggestedUpdatesEnabled: boolean;
  isPriorityRecommendationEnabled: boolean;
  isStatusRecommendationEnabled: boolean;
  defaultTextModel: string;
  defaultVisionModel: string;
  temperature: number;
  maxTokens: number;
  timeoutSeconds: number;
  retryCount: number;
  advisoryOnlyMode: boolean;
  allowStatusRecommendation: boolean;
  allowPriorityRecommendation: boolean;
  suggestionOnlyMode: boolean;
  confidenceThreshold: number;
  maxScreenshotAttachmentCount: number;
  lastModifiedByUserId?: number | null;
  lastModifiedByDisplayName?: string | null;
  lastModifiedDateUtc?: string | null;
}
