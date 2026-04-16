import type { RealtimeEvent } from "../types/realtime";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
} from "@microsoft/signalr";

const API_BASE_URL = import.meta.env.VITE_API_URL;
const REALTIME_PATH = `${API_BASE_URL}/realtime/hub`;

interface RealtimeConnectionOptions {
  getToken: () => Promise<string>;
  onEvent: (event: RealtimeEvent) => void;
  onError?: (error: unknown) => void;
}

interface RealtimeConnection {
  close: () => void;
}

type RealtimeEventLike = {
  eventType?: unknown;
  ticketId?: unknown;
  entityId?: unknown;
  actorUserId?: unknown;
  actorDisplayName?: unknown;
  recipientUserIds?: unknown;
  occurredDateUtc?: unknown;
  EventType?: unknown;
  TicketId?: unknown;
  EntityId?: unknown;
  ActorUserId?: unknown;
  ActorDisplayName?: unknown;
  RecipientUserIds?: unknown;
  OccurredDateUtc?: unknown;
};

function normalizeRealtimeEvent(value: unknown): RealtimeEvent | null {
  if (typeof value !== "object" || value === null) {
    return null;
  }

  const candidate = value as RealtimeEventLike;

  const eventTypeRaw = candidate.eventType ?? candidate.EventType;
  const occurredDateRaw = candidate.occurredDateUtc ?? candidate.OccurredDateUtc;
  const ticketIdRaw = candidate.ticketId ?? candidate.TicketId;
  const entityIdRaw = candidate.entityId ?? candidate.EntityId;
  const actorUserIdRaw = candidate.actorUserId ?? candidate.ActorUserId;
  const actorDisplayNameRaw =
    candidate.actorDisplayName ?? candidate.ActorDisplayName;
  const recipientUserIdsRaw =
    candidate.recipientUserIds ?? candidate.RecipientUserIds;

  if (
    typeof eventTypeRaw !== "string" ||
    eventTypeRaw.trim().length === 0 ||
    typeof occurredDateRaw !== "string"
  ) {
    return null;
  }

  const normalized: RealtimeEvent = {
    eventType: eventTypeRaw,
    occurredDateUtc: occurredDateRaw,
  };

  if (typeof ticketIdRaw === "string" && ticketIdRaw.trim().length > 0) {
    normalized.ticketId = ticketIdRaw;
  }

  if (typeof entityIdRaw === "string" && entityIdRaw.trim().length > 0) {
    normalized.entityId = entityIdRaw;
  }

  if (typeof actorUserIdRaw === "number" && Number.isFinite(actorUserIdRaw)) {
    normalized.actorUserId = actorUserIdRaw;
  }

  if (
    typeof actorDisplayNameRaw === "string" &&
    actorDisplayNameRaw.trim().length > 0
  ) {
    normalized.actorDisplayName = actorDisplayNameRaw;
  }

  if (
    Array.isArray(recipientUserIdsRaw) &&
    recipientUserIdsRaw.every((value) => typeof value === "number")
  ) {
    normalized.recipientUserIds = recipientUserIdsRaw;
  }

  return normalized;
}

export const realtimeService = {
  connect(options: RealtimeConnectionOptions): RealtimeConnection {
    let currentConnection: HubConnection | null = null;
    let reconnectTimer: number | null = null;
    let isClosed = false;
    let reconnectAttempt = 0;

    const clearReconnectTimer = () => {
      if (reconnectTimer !== null) {
        window.clearTimeout(reconnectTimer);
        reconnectTimer = null;
      }
    };

    const scheduleReconnect = () => {
      if (isClosed || reconnectTimer !== null) {
        return;
      }

      const delayMs = Math.min(15000, 2000 * Math.max(1, reconnectAttempt));
      reconnectTimer = window.setTimeout(() => {
        reconnectTimer = null;
        void connectInternal();
      }, delayMs);
    };

    const connectInternal = async () => {
      clearReconnectTimer();

      if (isClosed) {
        return;
      }

      try {
        const connection = new HubConnectionBuilder()
          .withUrl(REALTIME_PATH, {
            transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents,
            accessTokenFactory: () => options.getToken(),
          })
          .withAutomaticReconnect([0, 2000, 4000, 8000, 15000])
          .configureLogging(LogLevel.Warning)
          .build();

        connection.on("realtime", (message: unknown) => {
          try {
            const normalized = normalizeRealtimeEvent(message);
            if (!normalized) {
              return;
            }

            options.onEvent(normalized);
          } catch (error) {
            options.onError?.(error);
          }
        });

        connection.onreconnected(() => {
          reconnectAttempt = 0;
        });

        connection.onclose((error) => {
          if (isClosed) {
            return;
          }

          reconnectAttempt += 1;
          if (error) {
            options.onError?.(error);
          }
          scheduleReconnect();
        });

        await connection.start();

        if (isClosed) {
          await connection.stop();
          return;
        }

        currentConnection = connection;
        reconnectAttempt = 0;
      } catch (error) {
        if (currentConnection?.state === HubConnectionState.Connected) {
          await currentConnection.stop();
        }
        currentConnection = null;
        reconnectAttempt += 1;
        options.onError?.(error);
        scheduleReconnect();
      }
    };

    void connectInternal();

    return {
      close() {
        isClosed = true;
        clearReconnectTimer();
        const connection = currentConnection;
        currentConnection = null;
        if (connection) {
          void connection.stop();
        }
      },
    };
  },
};
