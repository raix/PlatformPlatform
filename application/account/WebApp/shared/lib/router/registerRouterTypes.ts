import type { Router } from "@tanstack/react-router";

import type { routeTree } from "./routeTree.generated";

// Type-level router registration only. Account routes run inside the host application's router
// (see federated-modules/routes.tsx), so no account router instance exists at runtime. This
// registration gives account code typed navigation and search parameters for its own routes.
declare module "@tanstack/react-router" {
  interface Register {
    router: Router<typeof routeTree>;
  }
  interface StaticDataRouteOption {
    trackingTitle?: string;
  }
}
