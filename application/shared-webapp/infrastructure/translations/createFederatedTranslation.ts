import type { Messages } from "@lingui/core";

import { type Locale, type LocaleFile, Translation } from "./Translation";

// Module federation container type
type FederatedContainer = {
  get(module: string): Promise<() => { messages: Messages }>;
};

// Cache for loaded translation modules
const translationModuleCache = new Map<string, Messages>();

/**
 * Configuration for federated translations
 * Each application that consumes federated modules should configure
 * which remotes might provide translations
 */
const FEDERATED_TRANSLATION_REMOTES = [
  "account"
  // Add more remotes here as they are created
] as const;

/**
 * Creates a Translation instance that automatically loads and merges translations
 * from all federated modules configured in the current application.
 *
 * This function:
 * 1. Uses the base translation loader for the host application
 * 2. Automatically discovers and loads translations from configured federated remotes
 * 3. Merges all translations together with remote translations taking precedence
 *
 * @param baseLoader - Function to load base translations for the host application
 * @returns Translation instance with federated translation support
 */
export function createFederatedTranslation(baseLoader: (locale: Locale) => Promise<LocaleFile>): Promise<Translation> {
  const federatedLoader = createFederatedLoader(baseLoader);
  return Translation.create(federatedLoader);
}

/**
 * Try to load translations from a federated module
 */
async function loadRemoteTranslations(remoteName: string, locale: Locale): Promise<Messages | null> {
  // Check cache first
  const cacheKey = `${remoteName}:${locale}`;
  const cached = translationModuleCache.get(cacheKey);
  if (cached) {
    return cached;
  }

  // Get container using RSBuild's naming convention (hyphens to underscores)
  const containerName = remoteName.replace(/-/g, "_");
  const container = (window as unknown as Record<string, unknown>)[containerName] as FederatedContainer | null;

  if (!container?.get) {
    return null;
  }

  try {
    const factory = await container.get(`./translations/${locale}`);
    const module = factory();

    if (module?.messages) {
      translationModuleCache.set(cacheKey, module.messages);
      return module.messages;
    }
  } catch {
    // Silently fail - the remote might not have translations for this locale
  }

  return null;
}

/**
 * Load translations from `@repo/ui` (the shared component library catalog).
 * These are merged underneath the host SPA's own messages so the host can override.
 *
 * `@repo/ui` is consumed as a built package (its `exports` map subpaths into `dist`), so a
 * templated dynamic import cannot be resolved into a scannable directory by the bundler. Each
 * locale is therefore imported explicitly; keep this map in sync with `i18n.config.ts`.
 */
const sharedCatalogLoaders: Record<Locale, () => Promise<{ messages?: Messages }>> = {
  "en-US": () => import("@repo/ui/translations/locale/en-US"),
  "da-DK": () => import("@repo/ui/translations/locale/da-DK")
};

async function loadSharedTranslations(locale: Locale): Promise<Messages | null> {
  try {
    const module = await sharedCatalogLoaders[locale]?.();
    return module?.messages ?? null;
  } catch {
    return null;
  }
}

/**
 * Creates a translation loader that merges translations from federated modules
 */
function createFederatedLoader(
  baseLoader: (locale: Locale) => Promise<LocaleFile>
): (locale: Locale) => Promise<LocaleFile> {
  return async (locale: Locale): Promise<LocaleFile> => {
    const [sharedMessages, baseMessages] = await Promise.all([loadSharedTranslations(locale), baseLoader(locale)]);

    // Precedence (last write wins): shared < own < federated remote
    const allMessages = { ...sharedMessages, ...baseMessages.messages };

    await Promise.all(
      FEDERATED_TRANSLATION_REMOTES.map(async (remoteName) => {
        const remoteMessages = await loadRemoteTranslations(remoteName, locale);
        if (remoteMessages) {
          Object.assign(allMessages, remoteMessages);
        }
      })
    );

    return { messages: allMessages };
  };
}
