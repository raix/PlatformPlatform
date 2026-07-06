import { pluginTypeCheck } from "@rsbuild/plugin-type-check";
import { defineConfig } from "@rslib/core";

export default defineConfig({
  source: {
    entry: {
      index: ["./**/*.tsx", "./**/*.ts", "!rslib.config.ts", "!node_modules/**", "!dist/**"]
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
    target: "web"
  },
  plugins: [pluginTypeCheck()]
});
