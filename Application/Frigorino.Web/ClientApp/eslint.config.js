import js from "@eslint/js";
import eslintConfigPrettier from "eslint-config-prettier";
import reactHooks from "eslint-plugin-react-hooks";
import { reactRefresh } from "eslint-plugin-react-refresh";
import globals from "globals";
import tseslint from "typescript-eslint";

export default tseslint.config([
    {
        ignores: ["dev-dist/**", "dist/**", "src/lib/api/**"],
    },
    {
        files: ["**/*.{ts,tsx}"],
        extends: [
            js.configs.recommended,
            tseslint.configs.recommended,
            // Frigorino is on React Compiler (vite.config.ts), so enable the full
            // react-hooks v7 rule set — the compiler rules (use-memo,
            // preserve-manual-memoization, set-state-in-effect, immutability, …)
            // catch the Rules-of-React violations the compiler can't safely optimize.
            reactHooks.configs.flat["recommended-latest"],
            reactRefresh.configs.vite({ allowExportNames: ["Route"] }),
            eslintConfigPrettier,
        ],
        languageOptions: {
            ecmaVersion: 2020,
            globals: globals.browser,
        },
    },
    {
        // TanStack Router file-routing convention: each route exports `Route`
        // plus a locally-declared component. react-refresh/only-export-components
        // flags the local component even with allowExportNames; HMR works via
        // @tanstack/router-plugin's transform anyway.
        files: ["src/routes/**/*.tsx"],
        rules: {
            "react-refresh/only-export-components": "off",
        },
    },
]);
