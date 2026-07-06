import { LinguiPlugin } from "@repo/build/plugin/LinguiPlugin";
import { pluginReact } from "@rsbuild/plugin-react";
import { pluginTypeCheck } from "@rsbuild/plugin-type-check";
import { defineConfig } from "@rslib/core";

export default defineConfig({
  source: {
    entry: {
      index: [
        "./**/*.tsx",
        "./**/*.ts",
        "./**/*.svg",
        "./**/*.css",
        "./**/*.png",
        "./**/*.webp",
        // The root Tailwind entry stylesheets are consumed directly via the package root exports
        // (`./theme.css`, `./tailwind.css`), not from `dist`; keep them out of the entry so rslib does
        // not emit dead, shadowed copies into `dist`.
        "!theme.css",
        "!tailwind.css",
        "!rslib.config.ts",
        "!node_modules/**",
        "!dist/**"
      ]
    }
  },
  lib: [
    {
      bundle: false,
      dts: true,
      format: "esm"
    }
  ],
  output: {
    target: "web",
    // Emit raster image assets (png/webp logos + hero images) unhashed into `dist/images` so they land
    // exactly where the package `exports` (`./*.png`, `./*.webp`) and component imports resolve, letting
    // rslib own image emission instead of a hand-rolled copy step.
    distPath: {
      image: "images"
    },
    filename: {
      image: "[name][ext]"
    }
  },
  plugins: [pluginReact(), pluginTypeCheck(), LinguiPlugin()]
});
