import type { Messages } from "@lingui/core";

// `@repo/ui` owns its own catalog. This loader is what makes the shared component library's
// translations self-contained: the package knows which locales it ships and how to load them, and the
// application wires it in via `<RepoUiTranslation>` (see @repo/infrastructure/translations). Keep the
// locale keys in sync with the shared i18n config.
const catalogs: Record<string, () => Promise<{ messages?: Messages }>> = {
  "en-US": () => import("./locale/en-US"),
  "da-DK": () => import("./locale/da-DK")
};

export async function loadUiTranslations(locale: string): Promise<{ messages: Messages }> {
  const module = await catalogs[locale]?.();
  return { messages: module?.messages ?? {} };
}
