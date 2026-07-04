import { i18n, type Messages } from "@lingui/core";

import type { Locale, LocalLoaderFunction } from "./Translation";

/**
 * The single point of coordination for translations across all self-contained systems.
 *
 * Only the global Lingui `i18n` object (shared as a module-federation singleton) and `globalThis` are
 * guaranteed to be single instances across remotes -- `@repo/*` packages are compiled per-remote, so a
 * module-level store would be duplicated. Every system registers a loader for its own catalog; catalogs
 * merge into one shared dictionary in registration order and activate on the shared `i18n`.
 */
type TranslationStore = {
  activeLocale: Locale | null;
  requestedLocale: Locale | null;
  loaders: Map<string, LocalLoaderFunction>;
  systemMessages: Map<string, Messages>;
  merged: Map<Locale, Messages>;
  systemLoaded: Map<string, Set<Locale>>;
  failed: Set<string>;
  loadInflight: Map<string, Promise<void>>;
  activateInflight: Map<string, Promise<void>>;
};

const store: TranslationStore = ((globalThis as Record<string, unknown>).__appTranslation ??= {
  activeLocale: null,
  requestedLocale: null,
  loaders: new Map(),
  systemMessages: new Map(),
  merged: new Map(),
  systemLoaded: new Map(),
  failed: new Set(),
  loadInflight: new Map(),
  activateInflight: new Map()
} satisfies TranslationStore) as TranslationStore;

export function registerCatalog(systemId: string, loader: LocalLoaderFunction): void {
  if (!store.loaders.has(systemId)) {
    store.loaders.set(systemId, loader);
  }
}

export function isLoaded(systemId: string, locale: Locale): boolean {
  return store.systemLoaded.get(systemId)?.has(locale) ?? false;
}

/** A prior load attempt failed; the Suspense gate renders untranslated instead of re-suspending forever. */
export function hasFailed(systemId: string, locale: Locale): boolean {
  return store.failed.has(`${systemId}:${locale}`);
}

function markLoaded(systemId: string, locale: Locale): void {
  let set = store.systemLoaded.get(systemId);
  if (!set) {
    set = new Set();
    store.systemLoaded.set(systemId, set);
  }
  set.add(locale);
}

/**
 * Rebuild the merged dictionary for a locale in loader-registration order (shared-ui < host < remotes),
 * so precedence is deterministic regardless of which dynamic import happens to resolve first.
 */
function rebuildMerged(locale: Locale): Messages {
  const merged: Messages = {};
  for (const systemId of store.loaders.keys()) {
    const messages = store.systemMessages.get(`${systemId}:${locale}`);
    if (messages) {
      Object.assign(merged, messages);
    }
  }
  store.merged.set(locale, merged);
  return merged;
}

/** Load a system's catalog for a locale (no activation). Idempotent; a failed load stays retryable. */
function loadSystemLocale(systemId: string, loader: LocalLoaderFunction, locale: Locale): Promise<void> {
  if (isLoaded(systemId, locale)) {
    return Promise.resolve();
  }
  const key = `${systemId}:${locale}`;
  const existing = store.loadInflight.get(key);
  if (existing) {
    return existing;
  }
  const promise = loader(locale)
    .then(({ messages }) => {
      store.systemMessages.set(key, messages);
      store.failed.delete(key);
      markLoaded(systemId, locale);
    })
    .catch((error) => {
      // A single system's catalog failing must not reject (that would crash the Suspense boundary or abort a
      // locale switch for every other system). Record the failure so the gate degrades to untranslated, but
      // do NOT mark it loaded -- the next locale activation retries it once the inflight entry is cleared.
      console.error(`Failed to load translations for "${systemId}" (${locale})`, error);
      store.failed.add(key);
    })
    .finally(() => {
      store.loadInflight.delete(key);
    });
  store.loadInflight.set(key, promise);
  return promise;
}

/** Load every registered system's catalog for the locale, then activate the merged dictionary once. */
export async function loadAllAndActivate(locale: Locale): Promise<void> {
  store.requestedLocale = locale;
  await Promise.all([...store.loaders].map(([systemId, loader]) => loadSystemLocale(systemId, loader, locale)));
  // A newer switch superseded this one while awaiting; let it win so a slow earlier load can't clobber it.
  if (store.requestedLocale !== locale) {
    return;
  }
  store.activeLocale = locale;
  document.documentElement.lang = locale;
  i18n.loadAndActivate({ locale, messages: rebuildMerged(locale) });
}

/** Merge a single system's catalog into the currently active locale (used when it mounts late). */
export function ensureSystemActive(systemId: string, loader: LocalLoaderFunction, locale: Locale): Promise<void> {
  const key = `${systemId}:${locale}`;
  const existing = store.activateInflight.get(key);
  if (existing) {
    return existing;
  }
  const promise = loadSystemLocale(systemId, loader, locale).then(() => {
    store.activateInflight.delete(key);
    if (store.activeLocale === locale) {
      i18n.loadAndActivate({ locale, messages: rebuildMerged(locale) });
    }
  });
  store.activateInflight.set(key, promise);
  return promise;
}
