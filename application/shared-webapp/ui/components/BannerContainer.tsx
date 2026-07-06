import { Suspense, useEffect, useRef } from "react";

type BannerContainerProps = {
  children: React.ReactNode;
};

function useBannerOffset(bannerRef: React.RefObject<HTMLDivElement | null>) {
  useEffect(() => {
    const element = bannerRef.current;
    if (!element) {
      return;
    }

    const updateOffset = () => {
      const height = element.offsetHeight;
      document.documentElement.style.setProperty("--banner-offset", `${height}px`);
    };

    updateOffset();

    const resizeObserver = new ResizeObserver(updateOffset);
    resizeObserver.observe(element);

    return () => {
      resizeObserver.disconnect();
      document.documentElement.style.setProperty("--banner-offset", "0rem");
    };
  }, [bannerRef]);
}

/**
 * The single owner of the fixed banner area at the top of the viewport.
 * Place this at the top of your app's root component and pass banners as children.
 *
 * Banners render as ordinary React children inside the container element, so the
 * element has exactly one writer and duplicate banners are structurally impossible.
 * Never render banner content into this element from anywhere else.
 *
 * The element is fixed at the top of the viewport.
 * z-40 ensures banners stay above mobile sticky header (z-30) during animations.
 * Measures the banner height and sets --banner-offset CSS variable for content positioning.
 */
export function BannerContainer({ children }: BannerContainerProps) {
  const bannerRef = useRef<HTMLDivElement>(null);
  useBannerOffset(bannerRef);

  return (
    <div ref={bannerRef} className="fixed top-0 right-0 left-0 z-40 [&_button]:w-fit">
      <Suspense>{children}</Suspense>
    </div>
  );
}
