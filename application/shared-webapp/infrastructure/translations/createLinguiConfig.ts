import type { LinguiConfig } from "@lingui/conf";

import { formatter } from "@lingui/format-po";

import i18nConfig from "./i18n.config";

export function createLinguiConfig(): LinguiConfig {
  return {
    locales: Object.keys(i18nConfig),
    sourceLocale: "en-US",
    catalogs: [
      {
        path: "<rootDir>/shared/translations/locale/{locale}",
        include: ["<rootDir>/**/*.ts", "<rootDir>/**/*.tsx"],
        exclude: ["**/node_modules/**", "**/dist", "**/*.d.ts", "**/*.test.*", "**/.*", "**/emails/**"]
      }
    ],
    format: formatter({ origins: false })
  };
}
