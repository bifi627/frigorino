import { fileURLToPath, URL } from "node:url";

import { tanstackRouter } from "@tanstack/router-plugin/vite";
import plugin from "@vitejs/plugin-react";
import child_process from "child_process";
import fs from "fs";
import path from "path";
import { env } from "process";
import { defineConfig } from "vite";
import { compression } from "vite-plugin-compression2";
import { VitePWA } from "vite-plugin-pwa";

const compressionExclude = [/\.(br|gz)$/, /\.(png|jpe?g|gif|webp|woff2?)$/i];

const target = env.VITE_PROXY_TARGET ?? "https://localhost:5001";

// PWA display name. Defaults to "Frigorino"; set VITE_APP_NAME per environment (e.g.
// "Frigorino (Stage)") to tell separately-installed stage/prod PWAs apart on a device.
// `||` (not `??`) so the Dockerfile's empty-string ARG default also falls back.
const appName = env.VITE_APP_NAME || "Frigorino";

// https://vitejs.dev/config/
export default defineConfig(({ command }) => ({
    plugins: [
        tanstackRouter({
            target: "react",
            autoCodeSplitting: true,
        }),
        VitePWA({
            strategies: "injectManifest",
            srcDir: "src",
            filename: "sw.ts",
            registerType: "autoUpdate",
            injectManifest: {
                // Firebase messaging SW imports push the bundle over the default 2 MiB limit.
                maximumFileSizeToCacheInBytes: 4 * 1024 * 1024,
            },
            devOptions: {
                enabled: true,
                type: "module",
            },
            manifest: {
                // `id` is the stable install identity, resolved relative to the origin
                // ("/" → https://<origin>/). Keeping it relative means the same build
                // deployed to stage and prod installs as two independent WebAPKs with no
                // collision. "/" also equals the previous implicit default (start_url),
                // so existing installs are preserved rather than duplicated.
                id: "/",
                name: appName,
                short_name: appName,
                description: "Frigorino",
                // Match the dark app theme (MUI dark `background.default` = #121212).
                // theme_color tints the Android standalone status bar; background_color
                // is the launch splash background — both were white, clashing with the app.
                theme_color: "#121212",
                background_color: "#121212",
                icons: [
                    // "any" = the transparent logo (browser tab, app lists).
                    {
                        src: "192.png",
                        sizes: "192x192",
                        type: "image/png",
                        purpose: "any",
                    },
                    {
                        src: "512.png",
                        sizes: "512x512",
                        type: "image/png",
                        purpose: "any",
                    },
                    // "maskable" = logo composited on the dark app background, so the
                    // Android launcher tile is #121212 instead of a white plate.
                    {
                        src: "maskable-192.png",
                        sizes: "192x192",
                        type: "image/png",
                        purpose: "maskable",
                    },
                    {
                        src: "maskable-512.png",
                        sizes: "512x512",
                        type: "image/png",
                        purpose: "maskable",
                    },
                ],
            },
        }),
        compression({
            algorithms: ["brotliCompress", "gzip"],
            exclude: compressionExclude,
        }),
        plugin(),
    ],
    resolve: {
        alias: {
            "@": fileURLToPath(new URL("./src", import.meta.url)),
        },
    },
    build: {
        outDir: "build",
    },
    server: {
        proxy: {
            "^/api/*": {
                target,
                secure: false,
            },
            "^/Account/*": {
                target,
                secure: false,
            },
            "^/openapi/*": {
                target,
                secure: false,
            },
            "^/scalar/*": {
                target,
                secure: false,
            },
            "^/healthz$": {
                target,
                secure: false,
            },
            "^/readyz$": {
                target,
                secure: false,
            },
        },
        port: Number(env.VITE_DEV_PORT) || 44375,
        https: command === "serve" ? loadDevCertHttps() : undefined,
    },
}));

function loadDevCertHttps() {
    const baseFolder =
        env.APPDATA !== undefined && env.APPDATA !== ""
            ? `${env.APPDATA}/ASP.NET/https`
            : `${env.HOME}/.aspnet/https`;

    const certificateName = "frigorino.client";
    const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
    const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

    if (!fs.existsSync(baseFolder)) {
        fs.mkdirSync(baseFolder, { recursive: true });
    }

    if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
        if (
            0 !==
            child_process.spawnSync(
                "dotnet",
                [
                    "dev-certs",
                    "https",
                    "--export-path",
                    certFilePath,
                    "--format",
                    "Pem",
                    "--no-password",
                ],
                { stdio: "inherit" },
            ).status
        ) {
            throw new Error("Could not create certificate.");
        }
    }

    return {
        key: fs.readFileSync(keyFilePath),
        cert: fs.readFileSync(certFilePath),
    };
}
