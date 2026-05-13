import React, { type ReactNode, useEffect, useRef } from "react";

import { useUserInfo } from "../auth/hooks";

interface TelemetryContext {
  userId?: string;
  tenantId?: string;
  sessionId: string;
}

const telemetryContext: TelemetryContext = {
  sessionId: generateSessionId()
};

function generateSessionId(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
}

function generateOperationId(): string {
  const bytes = new Uint8Array(8);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
}

/** Batches telemetry and sends to /api/track */
let pendingItems: TrackRequest[] = [];
let flushTimeout: ReturnType<typeof setTimeout> | null = null;

function enqueue(item: TrackRequest) {
  pendingItems.push(item);

  if (flushTimeout === null) {
    flushTimeout = setTimeout(flush, 1000);
  }
}

function flush() {
  flushTimeout = null;
  if (pendingItems.length === 0) return;

  const batch = pendingItems;
  pendingItems = [];

  navigator.sendBeacon("/api/track", JSON.stringify(batch));
}

// Flush on page unload
if (typeof window !== "undefined") {
  window.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "hidden") {
      flush();
    }
  });
}

interface TrackRequest {
  time: string;
  iKey: string;
  name: string;
  tags: Record<string, string>;
  data: {
    baseType: string;
    baseData: Record<string, unknown>;
  };
}

function buildTags(): Record<string, string> {
  const tags: Record<string, string> = {
    "ai.session.id": telemetryContext.sessionId,
    "ai.operation.id": generateOperationId(),
    "ai.device.type": "Browser"
  };

  if (telemetryContext.userId) {
    tags["ai.user.id"] = telemetryContext.userId;
  }
  if (telemetryContext.tenantId) {
    tags["ai.user.accountId"] = telemetryContext.tenantId;
  }

  return tags;
}

/** Lightweight telemetry client that posts to /api/track in the same format the backend expects. */
export const applicationInsights = {
  trackPageView(data: { name: string; uri?: string; properties?: Record<string, string> }) {
    enqueue({
      time: new Date().toISOString(),
      iKey: "webapp",
      name: "PageviewData",
      tags: buildTags(),
      data: {
        baseType: "PageviewData",
        baseData: {
          name: data.name,
          url: data.uri ?? window.location.href,
          properties: data.properties ?? {}
        }
      }
    });
  },

  trackException(data: { exception: Error; properties?: Record<string, string> }) {
    enqueue({
      time: new Date().toISOString(),
      iKey: "webapp",
      name: "ExceptionData",
      tags: buildTags(),
      data: {
        baseType: "ExceptionData",
        baseData: {
          severityLevel: "Error",
          exceptions: [
            {
              typeName: data.exception.name,
              message: data.exception.message,
              hasFullStack: !!data.exception.stack,
              stack: data.exception.stack ?? "",
              parsedStack: []
            }
          ],
          properties: data.properties ?? {}
        }
      }
    });
  },

  setAuthenticatedUserContext(userId: string, tenantId: string) {
    telemetryContext.userId = userId;
    telemetryContext.tenantId = tenantId;
  },

  clearAuthenticatedUserContext() {
    telemetryContext.userId = undefined;
    telemetryContext.tenantId = undefined;
  }
};

// Register global error handlers for unhandled exceptions
if (typeof window !== "undefined") {
  window.addEventListener("error", (event) => {
    if (event.error instanceof Error) {
      applicationInsights.trackException({ exception: event.error });
    }
  });

  window.addEventListener("unhandledrejection", (event) => {
    if (event.reason instanceof Error) {
      applicationInsights.trackException({ exception: event.reason });
    }
  });
}

const ErrorFallback = () => <h1>Something went wrong, please try again</h1>;

export interface ApplicationInsightsProviderProps {
  children: ReactNode;
}

export function ApplicationInsightsProvider({ children }: Readonly<ApplicationInsightsProviderProps>) {
  const userInfo = useUserInfo();
  if (userInfo?.isAuthenticated) {
    applicationInsights.setAuthenticatedUserContext(userInfo.id as string, userInfo.tenantId as string);
  } else {
    applicationInsights.clearAuthenticatedUserContext();
  }

  return <TelemetryErrorBoundary>{children}</TelemetryErrorBoundary>;
}

// Simple error boundary that catches render errors
class TelemetryErrorBoundary extends React.Component<
  { children: ReactNode },
  { hasError: boolean }
> {
  constructor(props: { children: ReactNode }) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error: Error) {
    applicationInsights.trackException({ exception: error });
  }

  render() {
    if (this.state.hasError) {
      return <ErrorFallback />;
    }
    return this.props.children;
  }
}

export type TrackingType = "page" | "menu" | "dialog" | "sidepane" | "interaction";
export type TrackingAction = "Open" | "Close" | "Submit" | "Cancel" | "Confirm";

export function trackInteraction(
  name: string,
  type: "page" | "interaction",
  action?: string,
  extraProperties?: Record<string, string>
): void;
export function trackInteraction(
  name: string,
  type: "menu" | "dialog" | "sidepane",
  action: TrackingAction,
  extraProperties?: Record<string, string>
): void;
export function trackInteraction(
  name: string,
  type: TrackingType,
  action?: string,
  extraProperties?: Record<string, string>
) {
  const typeSuffix = type === "interaction" ? "" : ` ${type}`;
  const displayName = action ? `${name} - ${action}${typeSuffix}` : name;
  applicationInsights.trackPageView({
    name: displayName,
    uri: window.location.href,
    properties: { type, ...extraProperties }
  });
}

// Register on window for cross-module-federation access.
(window as unknown as { __trackInteraction: typeof trackInteraction }).__trackInteraction = trackInteraction;

export function useTrackOpen(name: string, type: "menu" | "dialog" | "sidepane", isOpen = true, key?: string) {
  const prevOpen = useRef(false);
  const prevKey = useRef(key);
  useEffect(() => {
    const opened = isOpen && !prevOpen.current;
    const contentChanged = isOpen && prevOpen.current && key !== undefined && key !== prevKey.current;
    if (opened || contentChanged) {
      trackInteraction(name, type, "Open");
    }
    prevOpen.current = isOpen;
    prevKey.current = key;
  }, [isOpen, name, type, key]);
}

export function useTrackClose(name: string, type: "menu" | "dialog" | "sidepane", isOpen = true) {
  const prevOpen = useRef(false);
  useEffect(() => {
    if (!isOpen && prevOpen.current) {
      trackInteraction(name, type, "Close");
    }
    prevOpen.current = isOpen;
  }, [isOpen, name, type]);
}
