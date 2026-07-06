import { createRootRoute, Outlet } from "@tanstack/react-router";

// Code generation anchor only: account routes are re-parented into the host application's
// router (see federated-modules/routes.tsx), so this root route never renders.
export const Route = createRootRoute({ component: Outlet });
