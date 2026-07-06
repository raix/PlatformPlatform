import { i18n } from "@lingui/core";
import { useContext, useEffect } from "react";

import { AuthenticationContext } from "../auth/AuthenticationProvider";
import { persistPreferredLocale, preferredLocaleKey } from "./constants";
import { isLocale } from "./Translation";
import { translationContext } from "./TranslationContext";

export function useInitializeLocale() {
  const { userInfo } = useContext(AuthenticationContext);
  const { setLocale } = useContext(translationContext);

  useEffect(() => {
    if (userInfo?.isAuthenticated) {
      persistPreferredLocale(document.documentElement.lang);
    } else {
      // Anonymous: re-apply a stored preference only if it is a supported locale and not already active.
      // resolveInitialLocale already applies it before first paint; this covers an in-session transition
      // to anonymous (e.g. logout without a full reload). setLocale validates and sets <html lang>.
      const storedLocale = localStorage.getItem(preferredLocaleKey);
      if (storedLocale && isLocale(storedLocale) && storedLocale !== i18n.locale) {
        setLocale(storedLocale);
      }
    }
  }, [userInfo?.isAuthenticated, setLocale]);
}
