import { useState } from "react";
import { createPortal } from "react-dom";

import { withAccountTranslations } from "@/shared/translations/withAccountTranslations";

import ExpiringCardBanner from "./ExpiringCardBanner";
import InvitationBanner from "./InvitationBanner";
import PaymentFailedBanner from "./PaymentFailedBanner";
import "@repo/ui/tailwind.css";

function Banners() {
  const [target] = useState(() => document.getElementById("banner-root"));

  if (!target) {
    return null;
  }

  return createPortal(
    <>
      <InvitationBanner />
      <PaymentFailedBanner />
      <ExpiringCardBanner />
    </>,
    target
  );
}

export default withAccountTranslations(Banners);
