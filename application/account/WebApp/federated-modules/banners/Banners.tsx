import { withAccountTranslations } from "@/shared/translations/withAccountTranslations";

import ExpiringCardBanner from "./ExpiringCardBanner";
import InvitationBanner from "./InvitationBanner";
import PaymentFailedBanner from "./PaymentFailedBanner";
import "@repo/ui/tailwind.css";

// Rendered as a child of the host's BannerContainer, which owns the fixed banner area.
// Banners are ordinary React children of that container so the container has exactly one writer.
function Banners() {
  return (
    <>
      <InvitationBanner />
      <PaymentFailedBanner />
      <ExpiringCardBanner />
    </>
  );
}

export default withAccountTranslations(Banners);
