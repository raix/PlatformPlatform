import localeMap from "./i18n.config";

export const preferredLocaleKey = "preferred-locale";

// The default locale (first in the config). Selecting it clears any stored preference, so an absent
// `preferred-locale` entry always means "use the default" -- storage never holds a redundant default.
const defaultLocale = Object.keys(localeMap)[0];

/** Persist the preferred-locale choice, clearing it when it matches the default so absence == default. */
export function persistPreferredLocale(locale: string): void {
  if (locale === defaultLocale) {
    localStorage.removeItem(preferredLocaleKey);
  } else {
    localStorage.setItem(preferredLocaleKey, locale);
  }
}
