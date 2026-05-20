import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
    input: "./src/lib/openapi.json",
    output: "./src/lib/api",
    plugins: [
        "@hey-api/typescript",
        "@hey-api/sdk",
        "@hey-api/client-fetch",
        {
            name: "@tanstack/react-query",
            queryOptions: true,
            mutationOptions: true,
            queryKeys: {
                tags: true,
            },
        },
    ],
});
