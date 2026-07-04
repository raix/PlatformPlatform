export const preferredLocaleKey = "preferred-locale";

/** Persist the user's explicit locale choice so it survives reloads. Always store it -- an anonymous
 * user's server-rendered default is derived from Accept-Language (`UserInfo.GetValidLocale(browserLocale)`),
 * so it is not necessarily the config default; clearing "the default" would silently drop a real choice. */
export function persistPreferredLocale(locale: string): void {
  localStorage.setItem(preferredLocaleKey, locale);
}
