import type { LinguiConfig } from "@lingui/conf";

import { formatter } from "@lingui/format-po";

import i18nConfig from "../../../shared-webapp/infrastructure/translations/i18n.config";

const config: LinguiConfig = {
  locales: Object.keys(i18nConfig),
  sourceLocale: "en-US",
  catalogs: [
    {
      path: "<rootDir>/translations/locale/{locale}",
      // Include shared components so user-facing strings inside @repo/emails (e.g. <Footer>) extract
      // into each system's catalog. The email build only loads one system's catalog at render time,
      // so each system needs its own copy of any shared translations.
      include: [
        "<rootDir>/templates/**/*.tsx",
        "<rootDir>/templates/**/*.ts",
        "<rootDir>/../../../shared-webapp/emails/components/**/*.tsx",
        "<rootDir>/../../../shared-webapp/emails/components/**/*.ts"
      ],
      exclude: ["**/node_modules/**", "**/dist/**", "**/*.d.ts"]
    }
  ],
  format: formatter({ origins: false })
};

export default config;
