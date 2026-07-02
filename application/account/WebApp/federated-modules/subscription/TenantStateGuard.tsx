import type { ReactNode } from "react";

import { api, TenantState } from "@/shared/lib/api/client";
import { withAccountTranslations } from "@/shared/translations/withAccountTranslations";

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

export default withAccountTranslations(TenantStateGuard);
