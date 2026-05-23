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
            reactRefresh.configs.vite({ allowExportNames: ["Route"] }),
            eslintConfigPrettier,
        ],
        plugins: { "react-hooks": reactHooks },
        languageOptions: {
            ecmaVersion: 2020,
            globals: globals.browser,
        },
        rules: {
            // eslint-plugin-react-hooks v7's `flat.recommended` now bundles 14
            // React Compiler rules (use-memo, preserve-manual-memoization,
            // set-state-in-effect, etc.) that flag legitimate non-compiler code.
            // Frigorino isn't on React Compiler, so enable just the two stable
            // hooks rules. Re-evaluate when adopting the compiler.
            "react-hooks/rules-of-hooks": "error",
            "react-hooks/exhaustive-deps": "warn",
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
