import { loggedInPath } from "@repo/infrastructure/auth/constants";
import { useNavigate } from "@tanstack/react-router";
import { useCallback } from "react";

/**
 * Navigation helpers for routes owned by the main application (e.g. the dashboard).
 * These are ordinary navigations on the single shared router; the target paths are just not
 * part of account's own route tree, so they are addressed as plain strings.
 */
export function useMainNavigation() {
  const navigate = useNavigate();

  const navigateToMain = useCallback(
    (path: string) => {
      void navigate({ to: path });
    },
    [navigate]
  );

  const navigateToHome = useCallback(
    (returnPath?: string | null) => {
      void navigate({ to: returnPath ?? loggedInPath });
    },
    [navigate]
  );

  return {
    navigateToMain,
    navigateToHome
  };
}
