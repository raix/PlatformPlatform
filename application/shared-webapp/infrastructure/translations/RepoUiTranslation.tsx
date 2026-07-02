import { loadUiTranslations } from "@repo/ui/translations/loadUiTranslations";

import { createSystemTranslationProvider } from "./Translation";

// App-level provider that contributes `@repo/ui`'s own catalog to the shared translation dictionary.
// Applications render `<RepoUiTranslation>` around their tree so the shared component library's strings
// are registered and loaded — without the host having to know they exist. It lives here (not in
// `@repo/ui`) because the translation registry is in `@repo/infrastructure`, and `@repo/ui` sits below
// infrastructure in the dependency graph; `@repo/ui` owns the catalog and the loader, this owns the wiring.
export const RepoUiTranslation = createSystemTranslationProvider("shared-ui", loadUiTranslations);
