import { useContext, useEffect } from "react";

import type { Locale } from "./Translation";

import { AuthenticationContext } from "../auth/AuthenticationProvider";
import { persistPreferredLocale, preferredLocaleKey } from "./constants";
import { translationContext } from "./TranslationContext";

export function useInitializeLocale() {
  const { userInfo } = useContext(AuthenticationContext);
  const { setLocale } = useContext(translationContext);

  useEffect(() => {
    if (userInfo?.isAuthenticated) {
      persistPreferredLocale(document.documentElement.lang);
    } else {
      const storedLocale = localStorage.getItem(preferredLocaleKey) as Locale;
      if (storedLocale) {
        document.documentElement.lang = storedLocale;
        setLocale(storedLocale);
      }
    }
  }, [userInfo?.isAuthenticated, setLocale]);
}
