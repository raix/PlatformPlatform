// Single source of truth for the set of supported locales and their display metadata.
// `sourceLocale` is en-US; every locale listed here must have a `.po` catalog in each system.
const i18nConfig = {
  "en-US": { locale: "en-US", label: "English", territory: "US", rtl: false },
  "da-DK": { locale: "da-DK", label: "Dansk", territory: "DK", rtl: false }
};

export default i18nConfig;
