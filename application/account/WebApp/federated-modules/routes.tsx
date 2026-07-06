import { AuthenticationProvider } from "@repo/infrastructure/auth/AuthenticationProvider";
import { type AnyRoute, createRoute, Outlet } from "@tanstack/react-router";

import { withAccountTranslations } from "@/shared/translations/withAccountTranslations";

import { routeTree } from "../shared/lib/router/routeTree.generated";
import ErrorPage from "./errorPages/ErrorPage";
import NotFoundPage from "./errorPages/NotFoundPage";
import "@repo/ui/tailwind.css";

// The #account element scopes account CSS. The AuthenticationProvider must come from account's
// own bundle because @repo packages are compiled per system and context objects are not shared
// across the federation boundary.
function AccountLayout() {
  return (
    <div id="account" className="contents">
      <AuthenticationProvider>
        <Outlet />
      </AuthenticationProvider>
    </div>
  );
}

// The public update() type omits getParentRoute, but re-parenting through update() is exactly
// how the route generator itself assembles the tree.
type ReparentableRoute = AnyRoute & {
  update: (options: { getParentRoute: () => AnyRoute }) => unknown;
};

/**
 * Account's contribution to the host's single router: a pathless layout route carrying the
 * #account style scope, the account translation catalog gate, and account's error pages, with
 * every generated account route re-parented beneath it.
 *
 * The generated tree's own root exists only as a code generation anchor and never renders.
 */
export default function createAccountRouteTree(getParentRoute: () => AnyRoute): AnyRoute {
  const accountLayoutRoute = createRoute({
    id: "account-layout",
    getParentRoute,
    component: withAccountTranslations(AccountLayout),
    errorComponent: ErrorPage,
    notFoundComponent: NotFoundPage
  });

  const accountRoutes = (routeTree.children ?? []) as ReparentableRoute[];
  for (const route of accountRoutes) {
    route.update({ getParentRoute: () => accountLayoutRoute });
  }

  return accountLayoutRoute.addChildren(accountRoutes);
}
