import { type AnyRoute, createRouter } from "@tanstack/react-router";
import createAccountRouteTree from "account/routes";

import { routeTree } from "./routeTree.generated";

// Capture the host's own routes before grafting: the remote's generated tree is assembled
// against its own root and must never influence which routes the host owns.
const mainRoutes = [...((routeTree.children ?? []) as AnyRoute[])];
const accountRouteTree = createAccountRouteTree(() => routeTree) as AnyRoute;

// One router for the whole page: federated systems contribute route subtrees instead of
// mounting routers of their own, so exactly one router owns history and rendering.
export const router = createRouter({
  routeTree: routeTree.addChildren([...mainRoutes, accountRouteTree]),
  defaultPreload: "intent"
});

// Register router with tanstack/react-router
declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
  interface StaticDataRouteOption {
    trackingTitle?: string;
  }
}
