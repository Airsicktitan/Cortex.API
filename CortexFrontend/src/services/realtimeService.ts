import type { RealtimeEvent } from "../types/realtime";

const API_BASE_URL = import.meta.env.VITE_API_URL;
const REALTIME_PATH = `${API_BASE_URL}/realtime/stream`;

interface RealtimeConnectionOptions {
  getToken: () => Promise<string>;
  onEvent: (event: RealtimeEvent) => void;
  onError?: (error: unknown) => void;
}

interface RealtimeConnection {
  close: () => void;
}

export const realtimeService = {
  connect(options: RealtimeConnectionOptions): RealtimeConnection {
    let currentSource: EventSource | null = null;
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
        const token = await options.getToken();
        if (isClosed) {
          return;
        }

        const url = `${REALTIME_PATH}?access_token=${encodeURIComponent(token)}`;
        const source = new EventSource(url);
        currentSource = source;

        source.onopen = () => {
          reconnectAttempt = 0;
        };

        source.addEventListener("realtime", (message) => {
          reconnectAttempt = 0;

          try {
            const parsed = JSON.parse((message as MessageEvent<string>).data) as RealtimeEvent;
            options.onEvent(parsed);
          } catch (error) {
            options.onError?.(error);
          }
        });

        source.onerror = (error) => {
          currentSource?.close();
          currentSource = null;
          reconnectAttempt += 1;
          options.onError?.(error);
          scheduleReconnect();
        };
      } catch (error) {
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
        currentSource?.close();
        currentSource = null;
      },
    };
  },
};
