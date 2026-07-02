import { type Locale, withSystemTranslations } from "@repo/infrastructure/translations/Translation";

// Account's federated modules render inside other systems (e.g. main), where account has no bootstrap of
// its own. Wrapping each exposed module self-registers account's catalog into the shared translation
// dictionary and suspends until it is merged, so account strings are translated on first paint.
export const withAccountTranslations = withSystemTranslations(
  "account",
  (locale: Locale) => import(`@/shared/translations/locale/${locale}.ts`)
);
