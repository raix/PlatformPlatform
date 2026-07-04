import type React from "react";

import { i18n, type Messages } from "@lingui/core";
import { I18nProvider, useLingui } from "@lingui/react";
import { type ComponentType, Suspense, useEffect, useMemo, useState } from "react";

import { preferredLocaleKey } from "./constants";
import localeMap from "./i18n.config";
import { type TranslationContext as TranslationContextValue, translationContext } from "./TranslationContext";

export type Locale = keyof typeof localeMap;

export type LocaleInfo = {
  label: string;
  locale: string;
  territory: string;
  rtl: boolean;
};

export type LocaleMap = Record<Locale, LocaleInfo>;

export type LocaleFile = {
  messages: Messages;
};

export type LocalLoaderFunction = (locale: Locale) => Promise<LocaleFile>;

const TranslationContextProvider = translationContext.Provider;

export const locales = Object.keys(localeMap) as Locale[];

export function getLocaleInfo(locale: Locale): LocaleInfo {
  return localeMap[locale];
}

export function isLocale(value: string): value is Locale {
  return value in localeMap;
}

/**
 * The single point of coordination for translations across all self-contained systems.
 *
 * Only the global Lingui `i18n` object (shared as a module-federation singleton) and `globalThis`
 * are guaranteed to be single instances across remotes -- `@repo/*` packages are compiled per-remote,
 * so a module-level store would be duplicated. Every system registers a loader for its own catalog and
 * all catalogs merge into the one shared `i18n` dictionary.
 */
type TranslationStore = {
  activeLocale: Locale | null;
  loaders: Map<string, LocalLoaderFunction>;
  merged: Map<Locale, Messages>;
  systemLoaded: Map<string, Set<Locale>>;
  loadInflight: Map<string, Promise<void>>;
  activateInflight: Map<string, Promise<void>>;
};

const store: TranslationStore = ((globalThis as Record<string, unknown>).__appTranslation ??= {
  activeLocale: null,
  loaders: new Map(),
  merged: new Map(),
  systemLoaded: new Map(),
  loadInflight: new Map(),
  activateInflight: new Map()
} satisfies TranslationStore) as TranslationStore;

export function registerCatalog(systemId: string, loader: LocalLoaderFunction): void {
  if (!store.loaders.has(systemId)) {
    store.loaders.set(systemId, loader);
  }
}

function markLoaded(systemId: string, locale: Locale): void {
  let set = store.systemLoaded.get(systemId);
  if (!set) {
    set = new Set();
    store.systemLoaded.set(systemId, set);
  }
  set.add(locale);
}

function isLoaded(systemId: string, locale: Locale): boolean {
  return store.systemLoaded.get(systemId)?.has(locale) ?? false;
}

/** Load a system's catalog for a locale into the merged dictionary (no activation). Idempotent. */
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
      // Later merges win; the shared-ui loader is registered first so it stays lowest precedence, the host
      // next, and federated remotes register on mount so they layer on top -- matching the previous
      // "shared < own < remote" precedence.
      store.merged.set(locale, { ...store.merged.get(locale), ...messages });
    })
    .catch((error) => {
      // A single system's catalog failing to load must not reject: that would crash the Suspense boundary
      // in `SystemTranslationGate`, or abort a locale switch (`loadAllAndActivate`) for every other system.
      // Degrade to untranslated for just this system instead.
      console.error(`Failed to load translations for "${systemId}" (${locale})`, error);
    })
    .finally(() => {
      // Mark loaded on success and on failure alike, so a persistent load error degrades gracefully to
      // untranslated rather than re-suspending forever.
      markLoaded(systemId, locale);
    });
  store.loadInflight.set(key, promise);
  return promise;
}

/** Load every registered system's catalog for the locale, then activate the merged dictionary once. */
async function loadAllAndActivate(locale: Locale): Promise<void> {
  await Promise.all([...store.loaders].map(([systemId, loader]) => loadSystemLocale(systemId, loader, locale)));
  store.activeLocale = locale;
  i18n.loadAndActivate({ locale, messages: store.merged.get(locale) ?? {} });
}

/** Merge a single system's catalog into the currently active locale (used when it mounts late). */
function ensureSystemActive(systemId: string, loader: LocalLoaderFunction, locale: Locale): Promise<void> {
  const key = `${systemId}:${locale}`;
  const existing = store.activateInflight.get(key);
  if (existing) {
    return existing;
  }
  const promise = loadSystemLocale(systemId, loader, locale).then(() => {
    if (store.activeLocale === locale) {
      i18n.loadAndActivate({ locale, messages: store.merged.get(locale) ?? {} });
    }
  });
  store.activateInflight.set(key, promise);
  return promise;
}

/**
 * Resolve the initial locale. Authenticated users follow the server (`<html lang>`, set from the JWT);
 * anonymous users have their stored choice re-applied by `useInitializeLocale` after mount.
 */
function resolveInitialLocale(): Locale {
  const serverLocale = document.documentElement.lang;
  if (isLocale(serverLocale)) {
    return serverLocale;
  }
  if (import.meta.env.LOCALE && isLocale(import.meta.env.LOCALE)) {
    return import.meta.env.LOCALE;
  }
  return locales[0];
}

/** The default locale (first in the config). Selecting it clears any stored preference, so an absent
 * `preferred-locale` entry always means "use the default" -- storage never holds a redundant default. */
export const defaultLocale = locales[0];

/** Persist the preferred-locale choice, clearing it when it matches the default so absence == default. */
export function persistPreferredLocale(locale: Locale): void {
  if (locale === defaultLocale) {
    localStorage.removeItem(preferredLocaleKey);
  } else {
    localStorage.setItem(preferredLocaleKey, locale);
  }
}

/** Change the active locale for every registered system and persist the choice. */
export function setLocale(locale: string): Promise<void> {
  if (!isLocale(locale)) {
    return Promise.resolve();
  }
  persistPreferredLocale(locale);
  document.documentElement.lang = locale;
  return loadAllAndActivate(locale);
}

function TranslationProvider({ children }: { children: React.ReactNode }) {
  // Re-key the provider on locale change so the whole subtree remounts. Plain `t` macro strings read the
  // global i18n but don't subscribe to context updates, so without a remount their labels stay stale
  // after a locale switch while `<Trans>`/`useLingui` consumers update.
  const [currentLocale, setCurrentLocale] = useState(() => i18n.locale as Locale);

  useEffect(() => {
    const unsubscribe = i18n.on("change", () => setCurrentLocale(i18n.locale as Locale));
    return unsubscribe;
  }, []);

  useEffect(() => {
    const handleLocaleChangeRequest = (event: Event) => {
      void setLocale((event as CustomEvent).detail.locale);
    };
    document.addEventListener("locale-change-request", handleLocaleChangeRequest);
    return () => document.removeEventListener("locale-change-request", handleLocaleChangeRequest);
  }, []);

  const value: TranslationContextValue = useMemo(
    () => ({ currentLocale, setLocale, locales, getLocaleInfo }),
    [currentLocale]
  );

  return (
    <TranslationContextProvider value={value}>
      <I18nProvider key={currentLocale} i18n={i18n}>
        {children}
      </I18nProvider>
    </TranslationContextProvider>
  );
}

export const Translation = {
  /**
   * Bootstrap translations for a host SPA: register the shared-UI and host catalogs, then load and
   * activate the initial locale before the app renders.
   */
  async create(ownLoader: LocalLoaderFunction): Promise<{ TranslationProvider: typeof TranslationProvider }> {
    registerCatalog("host", ownLoader);
    await loadAllAndActivate(resolveInitialLocale());
    return { TranslationProvider };
  }
};

/**
 * Blocks its subtree until `systemId`'s catalog for the active locale has merged into the shared
 * dictionary — suspending (via a thrown promise) while it loads. Shared by the federated-module HOC and
 * the app-level package provider below.
 */
function SystemTranslationGate({
  systemId,
  loader,
  children
}: {
  systemId: string;
  loader: LocalLoaderFunction;
  children: React.ReactNode;
}) {
  const { i18n: activeI18n } = useLingui();
  const locale = activeI18n.locale as Locale;
  if (!isLoaded(systemId, locale)) {
    throw ensureSystemActive(systemId, loader, locale);
  }
  return <>{children}</>;
}

/**
 * Wrap a federated module so it contributes its own catalog to the shared dictionary. The system
 * self-registers (no host needs to know it exists) and suspends until its catalog for the active locale
 * is merged, preventing a flash of untranslated content inside the federated subtree.
 */
export function withSystemTranslations(systemId: string, loader: LocalLoaderFunction) {
  registerCatalog(systemId, loader);
  return function wrap<P extends object>(Component: ComponentType<P>) {
    return function WithSystemTranslations(props: P) {
      return (
        <Suspense fallback={null}>
          <SystemTranslationGate systemId={systemId} loader={loader}>
            <Component {...props} />
          </SystemTranslationGate>
        </Suspense>
      );
    };
  };
}

/**
 * Build an app-level provider that contributes a bundled shared-webapp package's own catalog to the
 * shared dictionary. Unlike a federated module (which decorates each exposed export), a bundled package
 * is used throughout the app, so the application renders this provider once around its tree. The package
 * owns its loader; this only registers it and gates rendering until it's merged.
 */
export function createSystemTranslationProvider(systemId: string, loader: LocalLoaderFunction) {
  registerCatalog(systemId, loader);
  return function SystemTranslationProvider({ children }: { children: React.ReactNode }) {
    return (
      <Suspense fallback={null}>
        <SystemTranslationGate systemId={systemId} loader={loader}>
          {children}
        </SystemTranslationGate>
      </Suspense>
    );
  };
}
