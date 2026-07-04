import type { ReactNode } from "react";

import { api, TenantState } from "@/shared/lib/api/client";

import SuspendedPage from "./SuspendedPage";

interface TenantStateGuardProps {
  children: ReactNode;
  pathname: string;
}

function TenantStateGuard({ children, pathname }: Readonly<TenantStateGuardProps>) {
  const { data: tenant } = api.useQuery("get", "/api/account/tenants/current");

  const isBillingPage = pathname.startsWith("/account/billing");

  if (tenant?.state === TenantState.Suspended && !isBillingPage) {
    return <SuspendedPage />;
  }

  return children;
}

// Not wrapped in withAccountTranslations: this guard has no translatable strings of its own and renders
// the HOST's children (main's <Outlet/>). Wrapping it would gate host content behind the account remote's
// catalog load. The one account UI it can render, <SuspendedPage>, self-wraps its own translations.
export default TenantStateGuard;
