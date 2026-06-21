# API Integration (frontend ↔ backend)

The SPA talks to the backend through a **fully generated** TypeScript client — types, SDK functions, and TanStack Query helpers are all emitted from the OpenAPI spec. There is no hand-written client and no hand-written `queryFn` / `mutationFn` / `queryKey`.

## Generation pipeline

One command from `ClientApp/`: `npm run api`.

1. `api:fetch` → `dotnet build ..` — the `Microsoft.Extensions.ApiDescription.Server` MSBuild target runs the app under a mock server and writes `src/lib/openapi.json` (configured via `OpenApiDocumentsDirectory` in `Frigorino.Web.csproj`). No backend boot, no DB.
2. `api:gen` → `openapi-ts` (config: `openapi-ts.config.ts`) reads `src/lib/openapi.json` and regenerates `src/lib/api/`.

hey-api plugins in use (`@hey-api/openapi-ts`): `@hey-api/typescript` (types), `@hey-api/sdk` (per-operation functions), `@hey-api/client-fetch` (the fetch client), and `@tanstack/react-query` with `queryOptions` / `mutationOptions` / `queryKeys.tags` enabled. Everything under `src/lib/api/` is generated **and committed** — never hand-edit it.

The loop is: change an endpoint or DTO → `npm run api` → consume the regenerated hook helpers. Nothing else to wire.

## The fetch client (`src/common/apiClient.ts`)

Configured once at app boot (imported for its side effect from `main.tsx`). There is **no `ClientApi` singleton** — generated SDK functions import the configured `client` internally.

```ts
import { getAuth } from "firebase/auth";
import { client } from "../lib/api/client.gen";

client.setConfig({ baseUrl: "" }); // same-origin — the SPA is served from wwwroot

client.interceptors.request.use(async (request) => {
    const token = await getAuth().currentUser?.getIdToken();
    if (token) request.headers.set("Authorization", `Bearer ${token}`);
    return request;
});
```

The Firebase bearer is attached via a **request interceptor**, not hey-api's `auth` resolver: ASP.NET's `AddOpenApi` emits no `securitySchemes` / per-operation `security`, so the `auth` hook would never fire. The interceptor runs on every call regardless of spec metadata.

## Consuming it — hook conventions

Every endpoint gets generated helpers (`getXOptions`, `xMutation`, `getXQueryKey`) in `src/lib/api/@tanstack/react-query.gen.ts`. Feature hooks under `features/<area>/use*.ts` spread those into `useQuery` / `useMutation`. Mirror the canonical files — `features/lists/useList.ts` (query) and `features/lists/useDeleteList.ts` (mutation). The full ruleset is in `CLAUDE.md` ("API hook conventions"); the load-bearing ones:

- Never write `queryFn`, `mutationFn`, or literal `queryKey` arrays — spread `getXOptions` / `xMutation` / `getXQueryKey()`.
- **No `*Keys.ts` factories.** Generated keys carry `tags`, supporting both point invalidation and tag-predicate invalidation (`predicate: q => (q.queryKey[0] as { tags?: string[] })?.tags?.includes('Households')`).
- Mutation hooks are arg-less; the caller passes `{ path, body }` to `mutate` / `mutateAsync`. Invalidation reads `variables.path.*` in `onSuccess`/`onSettled` and rebuilds keys via `getXQueryKey({ path })`. Optimistic hooks keep their own `onMutate`/`onError` plus an `onSuccess`/`onSettled` reconcile (e.g. create hooks swap the temp id for the server id in `onSuccess`) — codegen doesn't replace that substance.
- Enums arrive as **string unions** (the backend serializes enum names, not ints).
- hey-api throws the parsed error **response body** on non-2xx (the generated mutationFn passes `throwOnError: true`) — there is no `ApiError` class. Read field-level errors by narrowing the body: `(error as { errors?: { email?: string[] } } | null)?.errors?.email`. For display, type the local as `unknown` and use `error instanceof Error ? error.message : t("common.errorOccurred")`.

## Auth + household context on the wire

The JWT carries identity; the **active household** is server-side session state (`ICurrentHouseholdService`), not a header or claim — so switching households is a backend call, not a token refresh (`Firebase_Auth_Setup.md`, `Backend_Architecture.md`). Client requests are identical across households; the server resolves the active one from the session. The token itself is injected by the interceptor above; Firebase refreshes it transparently.
