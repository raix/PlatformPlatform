import type { RsbuildConfig, RsbuildPlugin } from "@rsbuild/core";

import { logger } from "@rsbuild/core";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

/**
 * File watcher ignore patterns.
 *
 * - Dist folders: ignore the WebApp's own dist AND every shared-webapp dist.
 *   Without the shared-webapp pattern, tsc -w writing to shared-webapp dist
 *   triggers rspack rebuilds that conflict with the source-change rebuild,
 *   creating an infinite rebuild loop where no stable output is produced.
 * - Tests and Playwright reports: never part of the dev bundle, so watching
 *   them wastes work and can cause spurious rebuilds when reports are generated.
 */
const applicationRoot = path.resolve(process.cwd(), "..", "..");
const distFolder = path.join(process.cwd(), "dist");
const ignoreDistPattern = `**/${path.relative(applicationRoot, distFolder)}/**`;
const ignoreSharedWebappDistPattern = "**/shared-webapp/*/dist/**";
const ignoreTestsPattern = "**/tests/**";
const ignorePlaywrightReportPattern = "**/playwright-report/**";

/**
 * Files to write to disk for the development server to serve
 */
const writeToDisk = ["index.html", "remoteEntry.js", "robots.txt", "favicon.ico"];

export type DevelopmentServerPluginOptions = {
  /**
   * The port to start the development server on
   */
  port: number;
  /**
   * Whether to reload the page when HMR fails to apply an update.
   * Disable for module federation remotes to prevent the remote's failed HMR
   * update from reloading the host page while the remote is still rebuilding.
   */
  liveReload?: boolean;
  /**
   * The HMR WebSocket path. Defaults to rsbuild's `/rsbuild-hmr`. Systems that share an origin with the
   * host (e.g. the `account` remote loaded into `main` under `app.dev.localhost`) must use a distinct
   * path so the gateway can route each system's WebSocket to its own dev server.
   */
  hmrPath?: string;
};

/**
 * RsBuild plugin to configure the development server to use the platformplatform.pfx certificate and
 * allow CORS for the platformplatform server.
 *
 * @param options - The options for the plugin
 */
export function DevelopmentServerPlugin(options: DevelopmentServerPluginOptions): RsbuildPlugin {
  return {
    name: "DevelopmentServerPlugin",
    setup(api) {
      api.modifyRsbuildConfig((userConfig, { mergeRsbuildConfig }) => {
        if (process.env.NODE_ENV === "production") {
          // Do not modify the Rsbuild config in production
          return userConfig;
        }

        // Path to the platformplatform.pfx certificate generated as part of the Aspire setup
        const pfxPath = path.join(os.homedir(), ".aspnet", "dev-certs", "https", "localhost.pfx");
        const passphrase = process.env.CERTIFICATE_PASSWORD ?? "";

        if (!fs.existsSync(pfxPath)) {
          throw new Error(`Certificate not found at path: ${pfxPath}`);
        }

        if (passphrase === "") {
          throw new Error("CERTIFICATE_PASSWORD environment variable is not set");
        }

        logger.info(`Using ignore pattern: ${ignoreDistPattern}`);

        const extraConfig: RsbuildConfig = {
          output: {
            // Force non-hashed filenames in dev mode. Without this, writeToDisk
            // writes content-hashed filenames (e.g. index.abc123.js) to dist/,
            // but the dev server serves non-hashed filenames (index.js) from
            // memory. The .NET backend reads index.html from disk, so the
            // hashed reference causes 404s and white screens.
            filename: {
              js: "[name].js",
              css: "[name].css"
            }
          },
          server: {
            // If the port is in use, the server will exit with an error
            strictPort: true,
            // Disable HTML fallback - the .NET backend handles SPA routing via MapFallback.
            // Without this, missing JS files (e.g. during failed rebuilds) return HTML instead
            // of 404, causing "Unexpected token '<'" errors and white screens.
            htmlFallback: false,
            // Allow CORS for the platformplatform server
            headers: {
              "Access-Control-Allow-Origin": "*"
            },
            // Start the server on the specified port with the platformplatform.pfx certificate
            port: options.port,
            https: {
              pfx: fs.readFileSync(pfxPath),
              passphrase
            }
          },
          dev: {
            // Leave client host/port unset so the HMR WebSocket targets the page's own origin -- the
            // AppGateway (app.dev.localhost:<gatewayPort> for main/account) or the BackOffice Kestrel --
            // which forwards the upgrade to this dev server, matching how every other dev asset is served.
            // rsbuild's client then uses location.host/port instead of pinning the dev server's own port,
            // which would bypass the gateway. Co-hosted systems (main + the account remote) share one
            // origin, so each passes a distinct hmrPath for the gateway to route each WebSocket on.
            client: options.hmrPath ? { path: options.hmrPath } : {},
            liveReload: options.liveReload ?? true,
            // Set publicPath to auto to enable the server to serve the files
            assetPrefix: "auto",
            // Write files to "dist" folder enabling the Api to serve them
            writeToDisk: (filename: string) => {
              return writeToDisk.some((file) => filename.endsWith(file));
            }
          },
          tools: {
            rspack: {
              watchOptions: {
                ignored: [
                  ignoreDistPattern,
                  ignoreSharedWebappDistPattern,
                  ignoreTestsPattern,
                  ignorePlaywrightReportPattern
                ]
              }
            }
          },
          performance: {
            printFileSize: {
              diff: true
            }
          }
        };

        return mergeRsbuildConfig(userConfig, extraConfig);
      });
    }
  };
}
