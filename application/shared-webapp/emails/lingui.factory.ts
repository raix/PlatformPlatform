import type { LinguiConfig } from "@lingui/conf";

import { formatter } from "@lingui/format-po";

import i18nConfig from "../infrastructure/translations/i18n.config";

// Factory used by every system's emails/lingui.config.ts. Lingui resolves <rootDir> relative to the
// directory containing the lingui.config file, which for emails is always <system>/WebApp/emails.
export function createEmailLinguiConfig(): LinguiConfig {
  return {
    locales: Object.keys(i18nConfig),
    sourceLocale: "en-US",
    catalogs: [
      {
        path: "<rootDir>/translations/locale/{locale}",
        include: ["<rootDir>/templates/**/*.tsx", "<rootDir>/templates/**/*.ts"],
        exclude: ["**/node_modules/**", "**/dist/**", "**/*.d.ts"]
      }
    ],
    format: formatter({ origins: false })
  };
}
